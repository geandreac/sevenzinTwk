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

-- ao criar um auth.users, cria o perfil (papel/nome vêm do user_metadata; padrão 'usuario')
create or replace function public.ao_criar_usuario()
returns trigger language plpgsql security definer
set search_path = public as $$
begin
  insert into public.perfis (id, email, nome, papel)
  values (
    new.id,
    new.email,
    coalesce(new.raw_user_meta_data->>'nome', split_part(new.email, '@', 1)),
    coalesce(new.raw_user_meta_data->>'papel', 'usuario')
  )
  on conflict (id) do nothing;
  return new;
end; $$;

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

-- Dica: promova a SUA conta a admin depois de criá-la pelo painel do Supabase (Auth → Users):
--   update public.perfis set papel = 'admin' where email = 'voce@exemplo.com';
