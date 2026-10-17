# Optional local AI assistance (CODEX-012)

## Permanent cost contract

CareerOS must remain usable with **zero mandatory financial cost**. AI is optional, disabled by default, and never gates existing matching, Skills Intelligence, documents, applications, interviews, tasks or analytics. No paid provider, subscription, API key, cloud fallback, additional package or migration is introduced. Existing PostgreSQL/Redis/web/API services suffice for all core features. Electricity and the user's existing hardware remain their own operating costs; no hardware purchase is required to use CareerOS.

## Provider evaluation and manual setup

Ollama was selected as an optional HTTP protocol integration because it supports local GGUF weights, CPU execution and structured JSON output. The integration uses .NET's built-in HttpClient, not an AI SDK. Ollama itself may support cloud models: loopback alone is **not** a privacy guarantee. CareerOS requires its cloud-disabled status API and rejects remote models before sending context. Older servers without that capability are honestly unavailable; there is no weaker compatibility fallback. See the [official local-only FAQ](https://docs.ollama.com/faq), [structured outputs](https://docs.ollama.com/capabilities/structured-outputs), [status client implementation](https://github.com/ollama/ollama/blob/main/api/client.go) and [model metadata contract](https://github.com/ollama/ollama/blob/main/api/types.go). Checked 2026-10-17; the experimental status API may change, in which case fail-closed unavailability is intentional.

Nothing is installed, enabled or downloaded automatically. To opt in manually:

1. Install Ollama from its official distribution after reviewing its terms. Keep it bound to **127.0.0.1:11434 on the API machine**; this integration deliberately does not accept arbitrary server URLs, remote hosts, Docker host aliases or DNS names.
2. Disable Ollama cloud features and restart the daemon. For a manually launched PowerShell server, use `$env:OLLAMA_NO_CLOUD='1'` and `$env:OLLAMA_HOST='127.0.0.1:11434'` before `ollama serve`. Desktop Ollama may need user environment settings and a full restart. `GET http://127.0.0.1:11434/api/status` must return `cloud.disabled: true`. If it does not, CareerOS sends no prompt.
3. Choose a small local instruction model and review its license, disk size and context support. Download only if you explicitly want it. As an evaluated low-disk example, [qwen2.5:0.5b](https://ollama.com/library/qwen2.5:0.5b) is listed at 398 MB with Apache 2.0 licensing. This is a download-size reference, **not** a measured RAM requirement or quality guarantee. A manual `ollama pull qwen2.5:0.5b` is an operator action; CareerOS never calls `/api/pull` or any model-management route. Small models can choose poor excerpts or produce invalid output.
4. Set API configuration `LocalAi:Model` to the installed explicit tag (for example `qwen2.5:0.5b`) and `LocalAi:Enabled` to true. In a PowerShell session launching the API:

   ```powershell
   $env:LocalAi__Model = 'qwen2.5:0.5b'
   $env:LocalAi__Enabled = 'true'
   dotnet run --project apps/api/src/CareerOS.Api
   ```

5. Open a saved job's detail page and choose **Optional local AI assistance**. Status checks inspect metadata only. Select an assistance type and explicitly confirm local processing before generation. Resume improvement requires selecting an owned saved resume and at least one recorded profile/resume fact.

To disable, unset the environment override or set `LocalAi__Enabled=false` and restart the API. The checked-in appsettings default remains false with no model tag. No Compose service was added and health/readiness does not depend on Ollama.

## Hardware and operational limits

CPU-only execution is permitted; compatible GPU support is optional. See [Ollama hardware support](https://docs.ollama.com/gpu) and its [memory/concurrency FAQ](https://docs.ollama.com/faq). There is no universal RAM minimum: model weights, quantization, context, KV cache, OS and other apps determine usage. Budget free RAM beyond model download size; if memory is limited, keep AI disabled. Large models can exhaust memory or take minutes and are outside this default experience. No Ollama command or loopback listener was detected on this machine, so no real model speed, quality or hardware-fit claim is made and no model was downloaded.

The request uses 8,192 context tokens, at most 1,200 predicted tokens, temperature zero, schema-constrained non-streaming output, disabled automatic truncation/context shifting and `keep_alive=0` to request unloading afterward. Prompt tokenization varies; if context does not fit, shorten the saved description/profile rather than silently dropping source text. One in-flight generation per API process, no inference queue, 90-second total generation deadline including preflight, three-second status deadline and two-second connection deadline. Client cancellation is propagated; Ollama is responsible for actually stopping its local computation. Six generation requests and 30 status checks per minute per client IP use the existing limiter/proxy policy; multiple local users share the IP quota. Do not expose this API directly without the existing authentication/proxy protections.

## Privacy and ownership

- `/api/v1/ai/status` and `POST /api/v1/jobs/{id}/assistance` require authentication. POST retains same-origin CSRF checks, strict DTO binding and request-size limits. Ownership is checked on both selected job and selected resume **before provider access**, including when AI is disabled.
- The provider destination is the literal `http://127.0.0.1:11434`. Proxies, redirects, cookies and credentials are disabled; clients cannot choose a provider URL, model, prompt or system instruction. Only `/api/status`, `/api/show` and `/api/chat` exist in the provider's code path.
- Every generation first verifies explicit cloud-disabled status, then local GGUF metadata with model_info and no remote_host/remote_model. Unverifiable metadata, cloud aliases, unsupported names, remote responses, tool calls, malformed output and incomplete responses are rejected. No prompt is sent on failed preflight. These controls assume a trusted local Ollama daemon and administrator; a malicious process impersonating localhost or a compromised daemon can lie. CareerOS cannot enforce the daemon's OS-level outbound firewall policy. Use network isolation/firewall controls for higher-assurance deployments, and never run a loopback reverse proxy to a cloud service in its place.
- Job/interview modes use only the saved title/description. Resume mode additionally reads only the user's saved profile headline/summary/years and explicit profile/selected-resume skill names. Source labels keep profile skills separate from resume evidence. No PDF bytes, filenames, storage paths, account names/email, locations, other resumes, interview notes or arbitrary document text are loaded into the prompt. User-supplied profile information is recorded evidence, not independently certified truth.
- Reads use a repeatable-read database snapshot, released before inference; no transaction remains open during model work. No prompt, response, chat history, generated text, metrics or score is persisted by CareerOS. The UI holds the current response in memory; navigation drops it. API responses inherit no-store. Existing logging records route/method/status/timing, not prompt/response contents; no HttpClientFactory logging or AI telemetry was added. A local daemon, browser extension, administrator or operator-enabled diagnostics has its own privacy boundary.

## Accuracy and usefulness

Job analysis is an **extractive summary**: the model chooses up to five responsibility quotes and five qualification quotes. Each must be an exact contiguous substring of the saved description and at most 300 characters. Unsupported or paraphrased claims are rejected, not disguised as facts. Interview questions combine model-selected exact excerpts with reviewed question templates (understanding, scenario, or conditional experience). Preparation actions come from a small reviewed catalog.

Resume improvement deliberately does **not** generate ready-to-paste career claims. The model prioritizes up to five saved fact IDs and reviewed actions: clarify wording, collect genuine evidence, or consider relevance. The server renders suggestions referencing the exact stored fact and explicitly asks for verification. It cannot add a skill, employer, qualification, achievement, experience or invented numerical result. Suggestions and quoted job requirements are separate from the displayed user-recorded facts. AI never changes documents, resumes, skills, matching scores, stages or applications.

Serialized source data is a single user-role JSON message; a fixed system message identifies it as untrusted data. No tools, browsing, shell, file access or agent loop is provided. Output is schema-constrained and independently validated (exact root keys, field types, array/item caps, known codes, known fact IDs, exact quotes, correct mode, at least one supported result). Invalid output gives 502 and is not shown. All UI content is escaped plain text; links/HTML from model/source text are not executed. Prompt injection may still influence which valid excerpts/actions are chosen; grounded validation prevents arbitrary model claims but does not guarantee relevance or correct classification. Human review remains necessary.

Input limits: saved description 1–10,000 characters; at most 100 profile skills and 100 selected resume skills; combined serialized context at most 24,000 UTF-8 bytes (including JSON escaping). No silent truncation. Response bytes are bounded even when Content-Length is absent: status 8 KiB, metadata 256 KiB, chat envelope 64 KiB. Overlimits are rejected. Missing/unknown data is never invented to fill gaps.

## Disabled and failure experience

Disabled status performs zero provider I/O. The assistance page explains optional setup and links to deterministic matching and Skills Intelligence; generation is disabled. No saved job description, no recorded resume/profile evidence, foreign/deleted IDs, invalid consent, oversize context, missing/unavailable server/model, cloud-enabled daemon, timeout, busy model and unverifiable output each have explicit states. Cancel and mode changes discard late responses. Saved records and all deterministic features remain usable regardless of AI failures. No cloud fallback or automatic paid purchase exists.

No model-quality benchmark, semantic fact checker, PDF parsing, CV rewriting, output persistence/export or automatic application is claimed. CODEX-013 is not started.
