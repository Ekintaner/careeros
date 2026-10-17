# CODEX-012 verification — optional local AI

Baseline: `6990b596590971073ac4f949a8b63c34df373abc` on the existing main checkout. Tracked files were initially clean. The pre-existing untracked Career_Command_Centre directory was not changed or added. No reset, discarded work, new migration, package, commit or push was performed. CODEX-013 is not started.

## Delivered behavior

Saved job details link to `/jobs/{id}/assistance` with job analysis, resume improvement and interview practice. LocalAi defaults to disabled with no configured model. Disabled status makes no provider calls; core matching/Skills Intelligence remain independent. Optional Ollama integration verifies cloud-disabled status and local GGUF metadata before context transmission, uses only literal loopback, disables proxies/redirects and accepts no client-supplied prompt/model/provider URL.

Job analysis selects exact source excerpts rather than generating unsupported factual summaries. Interview questions and resume suggestions use validated model selections and reviewed templates. Resume evidence is copied only from owned explicit records, with profile and selected-resume sources labeled separately. No PDF parsing, fabricated claims, profile mutation, automatic save or persisted chat history.

## Checks

| Check                                                | Result                                                                                          |
| ---------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| Backend unit suite, Release                          | 142 passed, zero skipped                                                                        |
| Real PostgreSQL 17 integration suite, Release        | 117 passed, zero skipped                                                                        |
| Frontend suite                                       | 116 passed                                                                                      |
| Locked .NET restore and Release solution compilation | Passed                                                                                          |
| Frontend lint / typecheck / production build         | Passed                                                                                          |
| Prettier                                             | Passed; unrelated pre-existing untracked Career_Command_Centre excluded                         |
| Compose configuration / tracked diff whitespace      | Passed                                                                                          |
| Existing migrations, schema snapshot and lockfiles   | Unchanged; integration verifies no pending model changes                                        |
| Browser                                              | Desktop, 768×1024 tablet and 390×844 mobile; no horizontal overflow; no console errors/warnings |
| Local services                                       | Frontend 3000, API 5080, PostgreSQL and Redis healthy                                           |
| GitHub submission                                    | Not performed; explicit user approval required                                                  |

Unit security checks cover consent/mode validation, model-name restrictions, cloud-enabled/unverifiable metadata, remote aliases, redirects, literal destination and preflight ordering, prompt role separation, source injection serialization, exact-quote grounding, unknown fact IDs/actions, wrong modes/extra fields, output byte bounds, cancellation, status timeout, a single generation slot and release on cancellation, remote/incomplete/tool-call envelopes, and disabled-provider zero I/O.

New PostgreSQL tests cover all three modes from actual saved data; user/job/resume ownership; anonymous and CSRF protection; strict request-field rejection; default disabled 503; missing description/evidence; character and UTF-8 context limits; unverifiable output 502; per-IP quota 429 with Retry-After; no provider calls on rejected requests; no PDF bytes/account identifiers in captured context; unchanged matching, skill versions and document download; and no pending schema changes. Test hosts force AI disabled unless explicitly replaced with a test provider, preventing developer machine configuration from contacting an actual model.

Frontend tests cover disabled/no-auto-generation, explicit consent, request shape, escaped untrusted text, selected resume and separate fact provenance, cancellation/late-result rejection, and honest provider errors. Browser checks additionally verify discoverable job entry point, all assistance types, expandable local setup/privacy guidance, empty resume state and disabled generation without configuration.

## Limits and evidence boundaries

No Ollama command or port-11434 listener was detected. No daemon or model was installed, downloaded or enabled. Successful inference, cancellation and failure protocol behavior were exercised with controlled HTTP handlers; PostgreSQL endpoint tests use a deterministic test provider. **Real model speed, semantic relevance and hardware fit were not measured.** Small models may fail strict validation; the application rejects their output rather than presenting ungrounded claims. Exact quotes can still be misclassified and valid selections can be influenced by source instructions; review is required.

Local-only guarantees rely on an honest local daemon/administrator; a malicious loopback process can lie about its metadata or cloud configuration. Unverifiable/older Ollama servers fail closed. Profile fields are user-recorded information, not independently verified credentials. No free-form CV rewrite, historical inference, document parsing, cloud fallback, web tools or persistence is provided. [Setup, hardware, privacy and complete boundaries](local-ai-assistance.md).

Browser work used the existing isolated UI verification account. A single synthetic company and job were added with explicit CODEX-012 UI test fixture labeling; they are not real employment records. No candidate profile skills, resumes, personal experience, applications or status history were created. Nonempty inference scenarios are verified in disposable test databases/frontend fixtures. Preview: http://localhost:3000/jobs/34df7e4c-be4f-411f-9ca7-3d86eeb40dee/assistance . Temporary viewport overrides were reset and the tab/services remain open.

A new GitHub Actions run cannot exist before submission; prior CODEX-011 CI is not claimed as verification of CODEX-012. The current changes remain local for user review and approval.

## Later combined submission authorization

The subsequent CODEX-013 user request explicitly authorizes one combined CODEX-012+013 commit and origin/main push after verification. The earlier approval hold above records the status at completion of CODEX-012; it is superseded by that later instruction. See verification-codex013.md for combined results and additional labelled browser fixtures.
