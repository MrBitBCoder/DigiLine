import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { writeFileSync, mkdirSync } from "node:fs";
import os from "node:os";
const base = process.env.DIGILINE_LOAD_URL ?? "http://localhost:5017";
if (!["localhost", "127.0.0.1"].includes(new URL(base).hostname)) throw new Error("Load runner is restricted to localhost verification environments.");
const duration = Number(process.env.DIGILINE_LOAD_SECONDS ?? 1800);
const output = process.env.DIGILINE_LOAD_OUTPUT ?? "test-results/load.json";
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const reads = [], writes = [], errors = [];
let requests = 0, events = 0, connected = 0;
async function api(path, token, body, measured = true) {
 const start = performance.now();
 try {
  const response = await fetch(base + path, { method: body === undefined ? "GET" : "POST", headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(30000) });
  if (!response.ok) throw new Error(`${path}: HTTP ${response.status}`);
  return response.status === 204 ? undefined : await response.json();
 } catch (e) { if (measured) errors.push(String(e.message)); else throw e; }
 finally { if (measured) { requests++; (body === undefined ? reads : writes).push(performance.now() - start); } }
}
const clinics = [];
for (let i = 0; i < 100; i++) {
 const auth = await api("/api/auth/login", null, { email: `load-${i}@verification.local`, password: "VerificationOnly123!" }, false);
 const details = await api("/api/doctor/sessions/active-upcoming", auth.token, undefined, false);
 if (!details) throw new Error(`No live session for load clinic ${i}; rerun fixture preparation.`);
 clinics.push({ token: auth.token, id: details.session.id });
}
const connections = [];
for (let batch = 0; batch < 20; batch++) {
 await Promise.all(Array.from({ length: 25 }, async (_, offset) => {
  const clinic = clinics[(batch * 25 + offset) % 100];
  const hub = new HubConnectionBuilder().withUrl(base + "/hubs/queue", { accessTokenFactory: () => clinic.token }).withAutomaticReconnect().configureLogging(LogLevel.Error).build();
  hub.on("QueueChanged", () => { events++; });
  await hub.start(); await hub.invoke("JoinSession", clinic.id); connections.push(hub); connected++;
 }));
}
console.log(`Connected ${connected} clients across ${clinics.length} clinics. Starting ${duration}s mixed workload.`);
const startedAt = new Date().toISOString(), end = Date.now() + duration * 1000;
const workers = Array.from({ length: 500 }, async (_, index) => {
 const clinic = clinics[index % 100]; await sleep(index * 7);
 while (Date.now() < end) {
  await api(index % 2 ? `/api/doctor/sessions/${clinic.id}?page=1&filter=Active` : "/api/doctor/sessions?page=1", clinic.token);
  await sleep(5000);
 }
});
const writers = clinics.map(async clinic => {
 while (Date.now() < end) {
  await api("/api/reception/queue/walk-in", clinic.token, { sessionId: clinic.id, walkInName: "Synthetic load patient", age: 30 });
  const called = await api(`/api/doctor/sessions/${clinic.id}/queue/call-next`, clinic.token, {});
  if (called?.id) await api(`/api/doctor/sessions/${clinic.id}/tokens/${called.id}/complete`, clinic.token, {});
  await sleep(10000);
 }
});
const progress = setInterval(() => console.log(`${requests} requests; ${errors.length} errors; ${Math.max(0, Math.round((end - Date.now())/1000))}s remaining`), 60000);
await Promise.all([...workers, ...writers]); clearInterval(progress);
await Promise.all(connections.map(c => c.stop()));
const p95 = values => values.sort((a,b) => a-b)[Math.floor(values.length * .95)] ?? null;
const result = { startedAt, finishedAt: new Date().toISOString(), durationSeconds: duration, clinics:100, historicalTokensPerClinic:100000, connectedClients:connected, requests, readCount:reads.length, writeCount:writes.length, readP95Ms:p95(reads), writeP95Ms:p95(writes), errors:errors.length, errorRate:errors.length/requests, errorExamples:errors.slice(0,10), events, hardware:{ platform:os.platform(),cpus:os.cpus().length,cpu:os.cpus()[0]?.model,memoryGiB:Math.round(os.totalmem()/2**30) } };
result.passed = result.readP95Ms < 500 && result.writeP95Ms < 1000 && result.errorRate < .01;
mkdirSync(output.substring(0, output.lastIndexOf("/")), { recursive:true }); writeFileSync(output,JSON.stringify(result,null,2)); console.log(JSON.stringify(result,null,2));
process.exitCode = result.passed ? 0 : 1;
