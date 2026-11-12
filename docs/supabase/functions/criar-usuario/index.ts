// Edge Function: criar-usuario
// O app Admin chama esta função (com o token do admin logado) para criar uma conta com senha definida.
// A chave service_role fica AQUI, no servidor — nunca no .exe. A função confere que o chamador é admin.
//
// Deploy:  supabase functions deploy criar-usuario
// Segredos usados (SUPABASE_URL / SUPABASE_ANON_KEY / SUPABASE_SERVICE_ROLE_KEY) já existem no ambiente das funções.

import { createClient } from "https://esm.sh/@supabase/supabase-js@2.45.4";

const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors });
  if (req.method !== "POST") return json({ erro: "método inválido" }, 405);

  const url = Deno.env.get("SUPABASE_URL")!;
  const anon = Deno.env.get("SUPABASE_ANON_KEY")!;
  const service = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

  // 1) identifica o chamador pelo token e confere se é admin (RLS protege a tabela perfis)
  const authHeader = req.headers.get("Authorization") ?? "";
  const comoChamador = createClient(url, anon, { global: { headers: { Authorization: authHeader } } });
  const { data: u } = await comoChamador.auth.getUser();
  if (!u?.user) return json({ erro: "não autenticado" }, 401);
  const { data: perfil } = await comoChamador.from("perfis").select("papel").eq("id", u.user.id).single();
  if (perfil?.papel !== "admin") return json({ erro: "apenas administradores podem criar contas" }, 403);

  // 2) cria o usuário com a service_role (servidor)
  const body = await req.json().catch(() => ({}));
  const email = String(body.email ?? "").trim();
  const senha = String(body.senha ?? "");
  const nome = body.nome ? String(body.nome) : null;
  const papel = body.papel === "admin" ? "admin" : "usuario";
  if (!email || senha.length < 12) return json({ erro: "e-mail e senha (mínimo 12) são obrigatórios" }, 400);

  const comoAdmin = createClient(url, service);
  const { data, error } = await comoAdmin.auth.admin.createUser({
    email,
    password: senha,
    email_confirm: true, // conta já criada pelo admin: entra direto, sem e-mail de confirmação
    user_metadata: { nome, papel },
  });
  if (error) return json({ erro: error.message }, 400);
  return json({ id: data.user?.id, email: data.user?.email });
});

function json(obj: unknown, status = 200): Response {
  return new Response(JSON.stringify(obj), { status, headers: { ...cors, "Content-Type": "application/json" } });
}
