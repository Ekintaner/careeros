import assert from "node:assert/strict";

// Run in the Codex browser JavaScript session, passing an already-bound native AX tab.
// Only the explicitly supplied synthetic interview may be edited. No credentials or APIs.
export async function verifyHardening(
  tab,
  { origin, interviewPath, syntheticFixture },
) {
  assert.equal(syntheticFixture, true, "Use a labelled synthetic fixture only");
  assert.match(origin, /^http:\/\/localhost:\d+$/);
  assert.match(interviewPath, /^\/interviews\/[a-f0-9-]{36}$/);
  const results = [];
  async function stateWith(pattern) {
    for (let attempt = 0; attempt < 20; attempt++) {
      const state = await tab.getAXState({ emit: false, disableDiffing: true });
      if (pattern.test(state)) return state;
    }
    throw new Error(`Browser did not reach ${pattern}`);
  }
  function index(state, pattern) {
    const line = state.split("\n").find((line) => pattern.test(line));
    assert.ok(line, `Missing control ${pattern}`);
    return Number(line.trim().match(/^\d+/)[0]);
  }
  const target = interviewPath + "?source=codex014-regression";
  await tab.goto(origin + "/session?returnTo=" + encodeURIComponent(target));
  let state = await stateWith(
    new RegExp(
      "URL: localhost:.*" + interviewPath + "\\?source=codex014-regression",
    ),
  );
  state = await stateWith(/date field .*Start, Value:/);
  results.push("Session recovery preserves detail and query");
  await tab.setValue(index(state, /pop up button .*Enter times in/), "UTC");
  state = await stateWith(/Enter times in, Value: UTC/);
  assert.match(state, /Start, Value: 2026-10-20T07:00/);
  assert.match(state, /End, Value: 2026-10-20T08:00/);
  results.push("Saved local date/time reloads and converts to UTC");
  await tab.setValue(
    index(state, /date field .*End, Value:/),
    "2026-10-20T06:00",
  );
  state = await tab.getAXState({ emit: false, disableDiffing: true });
  await tab.click(index(state, /\bbutton Save$/));
  state = await stateWith(/Interview end must be after start/);
  results.push("Invalid interval is rejected with accessible feedback");
  await tab.setValue(
    index(state, /date field .*End, Value:/),
    "2026-10-20T08:00",
  );
  state = await tab.getAXState({ emit: false, disableDiffing: true });
  await tab.click(index(state, /\bbutton Save$/));
  await stateWith(/text Saved\./);
  results.push("Corrected interval saves through real API and PostgreSQL");
  await tab.goto(
    origin + "/session?returnTo=" + encodeURIComponent("//attacker.example"),
  );
  await stateWith(/URL: localhost:\d+\/(?:\r?\n|$)/);
  results.push("External return target falls back to workspace root");
  await tab.goto(origin + interviewPath);
  await stateWith(/date field .*Start, Value: 2026-10-20T10:00/);
  return results;
}
