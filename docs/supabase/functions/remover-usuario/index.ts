// Edge Function: remover-usuario
// O app Admin chama esta função (com o token do admin logado) para EXCLUIR uma conta.
// A service_role fica no servidor. A função confere que o chamador é admin e que não está removendo a si mesmo.
//
// Deploy:  supabase functions deploy remover-usuario

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

  const authHeader = req.headers.get("Authorization") ?? "";
  const comoChamador = createClient(url, anon, { global: { headers: { Authorization: authHeader } } });
  const { data: u } = await comoChamador.auth.getUser();
  if (!u?.user) return json({ erro: "não autenticado" }, 401);
  const { data: perfil } = await comoChamador.from("perfis").select("papel").eq("id", u.user.id).single();
  if (perfil?.papel !== "admin") return json({ erro: "apenas administradores podem remover contas" }, 403);

  const body = await req.json().catch(() => ({}));
  const id = String(body.id ?? "").trim();
  if (!id) return json({ erro: "id do usuário é obrigatório" }, 400);
  if (id === u.user.id) return json({ erro: "você não pode remover a própria conta" }, 400);

  const comoAdmin = createClient(url, service);
  const { error } = await comoAdmin.auth.admin.deleteUser(id);
  if (error) return json({ erro: error.message }, 400);
  return json({ ok: true });
});

function json(obj: unknown, status = 200): Response {
  return new Response(JSON.stringify(obj), { status, headers: { ...cors, "Content-Type": "application/json" } });
}
