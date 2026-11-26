-- Sevenzin — esquema Supabase (Auth + RLS)
-- Rode no SQL Editor do seu projeto Supabase (uma vez). Idempotente onde possível.
-- As senhas ficam no Auth do Supabase (nunca aqui). Esta tabela guarda só papel/nome por usuário.

-- ───────────────────────── perfis (1:1 com auth.users) ─────────────────────────
create table if not exists public.perfis (
  id          uuid primary key references auth.users(id) on delete cascade,
  email       text not null,
  nome        text,
  papel       text not null default 'usuario' check (papel in ('admin','usuario')),
  criado_utc  timestamptz not null default now()
);
alter table public.perfis enable row level security;

-- helper: o usuário autenticado é admin? (security definer p/ não cair na própria RLS)
create or replace function public.e_admin()
returns boolean language sql security definer stable
set search_path = public as $$
  select exists (select 1 from public.perfis where id = auth.uid() and papel = 'admin');
$$;

-- RLS: cada um lê o próprio perfil; admin lê e gerencia todos
drop policy if exists "perfil: ler proprio ou admin" on public.perfis;
create policy "perfil: ler proprio ou admin" on public.perfis
  for select using (auth.uid() = id or public.e_admin());

drop policy if exists "perfil: admin gerencia" on public.perfis;
create policy "perfil: admin gerencia" on public.perfis
  for all using (public.e_admin()) with check (public.e_admin());

-- ao criar um auth.users, cria o perfil SEMPRE como 'usuario'. O papel NUNCA vem do user_metadata: antes vinha,
-- e qualquer cadastro com {"papel":"admin"} virava administrador.
create or replace function public.ao_criar_usuario()
returns trigger language plpgsql security definer
set search_path = public as $$
begin
  insert into public.perfis (id, email, nome, papel)
  values (
    new.id,
    new.email,
    coalesce(new.raw_user_meta_data->>'nome', split_part(new.email, '@', 1)),
    'usuario'
  )
  on conflict (id) do nothing;
  return new;
end; $$;

-- ADMINISTRADOR GLOBAL ÚNICO: o banco recusa papel 'admin' para qualquer e-mail que não seja o do dono,
-- venha do app, do painel, de uma função ou de SQL direto.
alter table public.perfis drop constraint if exists perfis_admin_unico;
alter table public.perfis add constraint perfis_admin_unico
  check (papel <> 'admin' or lower(email) = 'geandreac.dev@gmail.com');

-- Painel (não é SQL): Authentication → Sign In / Providers → "Allow new users to sign up" DESLIGADO.
-- Contas só são criadas pelo admin (app Admin → função criar-usuario, que usa a API administrativa).

drop trigger if exists trg_ao_criar_usuario on auth.users;
create trigger trg_ao_criar_usuario
  after insert on auth.users
  for each row execute function public.ao_criar_usuario();

-- ───────────────────────── acessos (dashboard) ─────────────────────────
create table if not exists public.acessos (
  id         bigint generated always as identity primary key,
  utc        timestamptz not null default now(),
  email      text,
  resultado  text,
  sucesso    boolean not null default false
);
alter table public.acessos enable row level security;

-- só admin lê o histórico; qualquer um (até anônimo, no login) pode registrar uma tentativa
drop policy if exists "acessos: admin le" on public.acessos;
create policy "acessos: admin le" on public.acessos for select using (public.e_admin());

drop policy if exists "acessos: registrar" on public.acessos;
create policy "acessos: registrar" on public.acessos for insert with check (true);

-- Promova a conta do dono a admin depois de criá-la pelo painel (Auth → Users). A regra perfis_admin_unico só
-- aceita este e-mail:
--   update public.perfis set papel = 'admin' where lower(email) = 'geandreac.dev@gmail.com';

-- ───────────────────────── vínculo da conta ao computador (HWID) + sessão única ─────────────────────────
-- Cada conta de usuário fica presa a UM computador. O app envia só hashes SHA-256 (nunca o identificador bruto):
--   p_hwid         hash do conjunto de componentes
--   p_componentes  hashes das peças FORTES: UUID e serial da placa-mãe (o app só cai para MachineGuid e ID da CPU
--                  quando a placa não traz identificador real; ver IdentidadeDispositivo.cs)
-- É o mesmo PC se o hwid for igual OU se pelo menos 2 componentes coincidirem (com as 2 peças fortes, as duas
-- precisam bater: reinstalar o Windows ou trocar a CPU não muda nada; trocar a placa-mãe exige liberação do admin). Cada vínculo bem-sucedido gera uma sessao_id nova: o login mais recente derruba
-- o anterior (sessão única). Admin é isento. Só as funções abaixo escrevem na tabela (sem policy de escrita).

create table if not exists public.dispositivos (
  usuario_id     uuid primary key references auth.users(id) on delete cascade,
  hwid           text not null,
  componentes    text[] not null default '{}',
  vinculado_utc  timestamptz not null default now(),
  ultimo_utc     timestamptz not null default now(),
  sessao_id      uuid,
  sessao_utc     timestamptz
);
alter table public.dispositivos enable row level security;

drop policy if exists "dispositivos: admin le" on public.dispositivos;
create policy "dispositivos: admin le" on public.dispositivos for select using (public.e_admin());
-- sem policy de insert/update/delete: nem o próprio usuário consegue apagar ou trocar o vínculo pela API REST.

create or replace function public.registrar_dispositivo(p_hwid text, p_componentes text[])
returns jsonb language plpgsql security definer
set search_path = public as $$
declare
  v_uid    uuid := auth.uid();
  v_atual  public.dispositivos;
  v_iguais int;
  v_minimo int;
  v_sessao uuid := gen_random_uuid();
begin
  if v_uid is null then raise exception 'não autenticado'; end if;
  if p_hwid is null or p_hwid !~ '^[0-9a-f]{64}$' then raise exception 'hwid inválido'; end if;
  if coalesce(array_length(p_componentes, 1), 0) not between 1 and 8
     or exists (select 1 from unnest(p_componentes) c where c !~ '^[0-9a-f]{64}$') then
    raise exception 'componentes inválidos';
  end if;
  if public.e_admin() then return jsonb_build_object('status', 'admin'); end if;

  select * into v_atual from public.dispositivos where usuario_id = v_uid for update;
  if not found then
    insert into public.dispositivos (usuario_id, hwid, componentes, sessao_id, sessao_utc)
    values (v_uid, p_hwid, p_componentes, v_sessao, now());
    return jsonb_build_object('status', 'vinculado', 'sessao', v_sessao);
  end if;

  select count(*) into v_iguais from unnest(p_componentes) c where c = any (v_atual.componentes);
  v_minimo := least(2, coalesce(array_length(v_atual.componentes, 1), 0));
  if v_atual.hwid <> p_hwid and v_iguais < v_minimo then
    insert into public.acessos (email, resultado, sucesso)
    values ((select email from auth.users where id = v_uid), 'Bloqueado: outro computador', false);
    return jsonb_build_object('status', 'outro_dispositivo');
  end if;

  -- mesmo PC: atualiza os hashes (acompanha troca de peça) e abre a sessão nova, derrubando a anterior
  update public.dispositivos
     set hwid = p_hwid, componentes = p_componentes, ultimo_utc = now(), sessao_id = v_sessao, sessao_utc = now()
   where usuario_id = v_uid;
  return jsonb_build_object('status', 'ok', 'sessao', v_sessao);
end; $$;

create or replace function public.validar_sessao(p_sessao uuid, p_hwid text)
returns boolean language plpgsql security definer
set search_path = public as $$
begin
  if auth.uid() is null then return false; end if;
  if public.e_admin() then return true; end if;
  update public.dispositivos set ultimo_utc = now()
   where usuario_id = auth.uid() and sessao_id = p_sessao and hwid = p_hwid;
  return found;
end; $$;

create or replace function public.resetar_dispositivo(p_usuario uuid)
returns void language plpgsql security definer
set search_path = public as $$
begin
  if not public.e_admin() then raise exception 'apenas administradores podem liberar computadores'; end if;
  delete from public.dispositivos where usuario_id = p_usuario;
  insert into public.acessos (email, resultado, sucesso)
  values ((select email from auth.users where id = p_usuario), 'Computador liberado pelo admin', true);
end; $$;

revoke all on function public.registrar_dispositivo(text, text[]) from public, anon;
revoke all on function public.validar_sessao(uuid, text) from public, anon;
revoke all on function public.resetar_dispositivo(uuid) from public, anon;
grant execute on function public.registrar_dispositivo(text, text[]) to authenticated;
grant execute on function public.validar_sessao(uuid, text) to authenticated;
grant execute on function public.resetar_dispositivo(uuid) to authenticated;
