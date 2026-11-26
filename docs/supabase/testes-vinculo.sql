-- Testes do vínculo HWID + sessão única (docs/supabase/schema.sql).
-- Rode no SQL Editor do Supabase DEPOIS de aplicar o schema. Tudo roda dentro de uma transação que termina em
-- ROLLBACK: nada fica gravado. As duas contas de teste (não-admin) são criadas aqui mesmo e somem no ROLLBACK,
-- então nenhuma conta real é tocada. Cada bloco imprime "OK - ..." ou derruba a transação com "FALHOU - ...".

begin;

do $$
declare
  u1 uuid := gen_random_uuid(); -- conta de teste A (temporária)
  u2 uuid := gen_random_uuid(); -- conta de teste B (temporária)
  pc_a  text[] := array[repeat('a',64), repeat('b',64), repeat('c',64), repeat('d',64)];
  pc_b  text[] := array[repeat('1',64), repeat('2',64), repeat('3',64), repeat('4',64)];
  r jsonb; s1 uuid; s2 uuid; v boolean;
begin
  -- o gatilho trg_ao_criar_usuario cria o perfil de cada uma como 'usuario'
  insert into auth.users (id, instance_id, aud, role, email, encrypted_password, created_at, updated_at,
                          raw_app_meta_data, raw_user_meta_data)
  values (u1, '00000000-0000-0000-0000-000000000000', 'authenticated', 'authenticated',
          'teste-vinculo-a@sevenzin.invalid', '', now(), now(), '{}'::jsonb, '{}'::jsonb),
         (u2, '00000000-0000-0000-0000-000000000000', 'authenticated', 'authenticated',
          'teste-vinculo-b@sevenzin.invalid', '', now(), now(), '{}'::jsonb, '{}'::jsonb);
  if exists (select 1 from public.perfis where id in (u1, u2) and papel <> 'usuario') then
    raise exception 'FALHOU - contas de teste deveriam ser comuns';
  end if;

  -- age como a conta A, autenticada
  perform set_config('request.jwt.claims', json_build_object('sub', u1, 'role', 'authenticated')::text, true);
  set local role authenticated;

  -- 1. primeiro login vincula
  r := public.registrar_dispositivo(repeat('0',63) || 'a', pc_a);
  if r->>'status' <> 'vinculado' then raise exception 'FALHOU - 1º login deveria vincular: %', r; end if;
  s1 := (r->>'sessao')::uuid;
  raise notice 'OK - 1º login vinculou o PC A';

  -- 2. mesmo PC entra de novo e ganha sessão nova; a sessão anterior deixa de valer (sessão única)
  r := public.registrar_dispositivo(repeat('0',63) || 'a', pc_a);
  if r->>'status' <> 'ok' then raise exception 'FALHOU - mesmo PC deveria entrar: %', r; end if;
  s2 := (r->>'sessao')::uuid;
  if public.validar_sessao(s1, repeat('0',63) || 'a') then raise exception 'FALHOU - sessão antiga continuou válida'; end if;
  if not public.validar_sessao(s2, repeat('0',63) || 'a') then raise exception 'FALHOU - sessão nova deveria valer'; end if;
  raise notice 'OK - login novo derrubou a sessão anterior';

  -- 3. troca de UMA peça (3 de 4 componentes iguais): continua liberado e o vínculo acompanha
  r := public.registrar_dispositivo(repeat('0',63) || 'b', array[repeat('a',64), repeat('b',64), repeat('c',64), repeat('9',64)]);
  if r->>'status' <> 'ok' then raise exception 'FALHOU - troca de 1 peça não deveria bloquear: %', r; end if;
  raise notice 'OK - troca de uma peça manteve o acesso';

  -- 4. outro computador (nenhum componente igual): bloqueado
  r := public.registrar_dispositivo(repeat('0',63) || 'c', pc_b);
  if r->>'status' <> 'outro_dispositivo' then raise exception 'FALHOU - outro PC deveria ser bloqueado: %', r; end if;
  raise notice 'OK - outro computador foi bloqueado';

  -- 5. só 1 componente igual (o mínimo é 2): bloqueado
  r := public.registrar_dispositivo(repeat('0',63) || 'd', array[repeat('a',64), repeat('5',64), repeat('6',64), repeat('7',64)]);
  if r->>'status' <> 'outro_dispositivo' then raise exception 'FALHOU - 1 componente igual deveria bloquear: %', r; end if;
  raise notice 'OK - coincidência de um só componente foi bloqueada';

  -- 6. entrada inválida é recusada (proteção contra lixo e injeção)
  begin
    perform public.registrar_dispositivo('nao-e-hash', pc_a);
    raise exception 'FALHOU - hwid inválido foi aceito';
  exception when others then
    if sqlerrm like 'FALHOU%' then raise; end if;
    raise notice 'OK - hwid inválido recusado (%)', sqlerrm;
  end;

  -- 7. o usuário não consegue apagar nem trocar o próprio vínculo pela API (RLS sem policy de escrita)
  delete from public.dispositivos where usuario_id = u1;
  update public.dispositivos set componentes = pc_b where usuario_id = u1;
  reset role;
  if not exists (select 1 from public.dispositivos where usuario_id = u1 and componentes <> pc_b) then
    raise exception 'FALHOU - usuário alterou o próprio vínculo';
  end if;
  raise notice 'OK - usuário não apaga nem altera o próprio vínculo';

  -- 8. conta comum não pode liberar PC (nem o próprio, nem de outro)
  perform set_config('request.jwt.claims', json_build_object('sub', u2, 'role', 'authenticated')::text, true);
  set local role authenticated;
  begin
    perform public.resetar_dispositivo(u1);
    raise exception 'FALHOU - conta comum liberou um PC';
  exception when others then
    if sqlerrm like 'FALHOU%' then raise; end if;
    raise notice 'OK - conta comum não libera PC (%)', sqlerrm;
  end;

  -- 9. conta B não enxerga o vínculo da conta A
  if exists (select 1 from public.dispositivos where usuario_id = u1) then
    raise exception 'FALHOU - conta B leu o vínculo da conta A';
  end if;
  raise notice 'OK - uma conta não lê o vínculo de outra';
  reset role;
end $$;

rollback;
