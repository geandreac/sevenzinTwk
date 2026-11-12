# Contas via Supabase (Auth + RLS)

Backend de contas do Sevenzin usando **Supabase Auth** (login/senha/sessão JWT, reset de senha, e-mail) + **RLS**.
Modelo seguro para app cliente: o `.exe` embarca só a **chave `anon` (pública)**; a segurança fica nas políticas RLS e
a criação de contas pelo admin passa por uma **Edge Function** (a chave `service_role` nunca sai do servidor).

Arquivos:
- [`schema.sql`](schema.sql) — tabela `perfis` (papel/nome por usuário) + `acessos` (dashboard) + RLS + trigger.
- [`functions/criar-usuario/index.ts`](functions/criar-usuario/index.ts) — Edge Function: admin cria conta com senha.

## Passo a passo (criar o projeto)

1. Crie um projeto em **supabase.com** (plano gratuito serve). Anote, em **Project Settings → API**:
   - **Project URL** (ex.: `https://xxxx.supabase.co`)
   - **anon public key** (pode ir no app)
   - **service_role key** (SECRETA — só no servidor/Edge Function, nunca no app)
2. **Auth → Providers → Email**: deixe **Email** habilitado. Como o admin cria as contas já confirmadas
   (`email_confirm: true` na função), o usuário entra direto; você pode manter "Confirm email" ligado para auto-cadastros.
3. **SQL Editor**: cole e rode [`schema.sql`](schema.sql).
4. Crie a **sua** conta (Auth → Users → Add user, ou pelo login do app depois) e promova a admin:
   ```sql
   update public.perfis set papel = 'admin' where email = 'voce@exemplo.com';
   ```
5. **Edge Function** (precisa da CLI do Supabase):
   ```bash
   supabase link --project-ref SEU_REF
   supabase functions deploy criar-usuario
   ```
   Os segredos `SUPABASE_URL` / `SUPABASE_ANON_KEY` / `SUPABASE_SERVICE_ROLE_KEY` já existem no ambiente das funções.
6. Configure o app (variáveis de ambiente — a `anon` é pública, mas mantemos fora do código):
   ```powershell
   setx SEVENZIN_SUPABASE_URL  "https://xxxx.supabase.co"
   setx SEVENZIN_SUPABASE_ANON "a_chave_anon_publica"
   ```

## Como o app vai usar (próxima etapa — eu implemento após o projeto existir)

Troca da autenticação local pelo Supabase, lendo URL/anon das env vars acima:

- **Login** (os dois apps): `Supabase` C# client → `Auth.SignInWithPassword(email, senha)`. Guarda a sessão (JWT
  access + refresh). "Esqueci minha senha" passa a funcionar de verdade (`Auth.ResetPasswordForEmail`).
- **Papel (admin × usuário)**: lido da tabela `perfis` (RLS garante que usuário só vê o próprio; admin vê todos).
  O app Admin exige `papel = 'admin'` — igual hoje, mas validado pelo servidor.
- **Admin cria login**: chama a Edge Function `criar-usuario` com o token do admin (não embarca `service_role`).
- **Dashboard de acessos**: lê a tabela `acessos` (admin só) — cada login insere um registro; métricas e histórico
  como já estão hoje, mas vindos do banco (compartilhado entre máquinas).
- **Migração**: um passo único para subir as contas atuais do JSON para o Supabase (via Edge Function/admin).
- **Fallback**: se as env vars não estiverem definidas, o app continua no JSON local (como hoje) — nada trava.

### O que muda vs. hoje
- A autenticação deixa de ser a nossa (`ServicoAutenticacao`/bcrypt local) e passa a ser a do Supabase (hash e sessão
  no servidor). O 2FA passaria a ser o do Supabase (TOTP/MFA) numa etapa seguinte, se quiser.
- Exige internet para logar. O otimizador em si continua funcionando offline depois de autenticado (sessão em cache).

> Status: este diretório é o **kit do backend**. A integração no app .NET entra depois que o projeto Supabase existir
> e as env vars estiverem definidas (para eu conseguir compilar e você validar o login real).
