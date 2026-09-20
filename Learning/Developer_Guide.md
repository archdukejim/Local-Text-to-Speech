# Developer's Guide — the Local TTS broker API

Local TTS is a speech **service**. Your mod hands over text (and, if you like, a voice and a few
parameters); Local TTS synthesizes on its own background thread, stages the finished audio as a
WAV file, and tells you where it is and when it's ready. Your mod gets a **file path** — it never
references Local TTS's assembly, and Local TTS never references yours.

Everything is one public static class:

```
LocalTts.LocalTtsBroker          (assembly: LocalTts.dll)
```

Because it's a plain static class with primitive parameters, the recommended integration is
**reflection** — no hard dependency, no load-order requirement. If Local TTS is absent, your
resolve simply fails and you fall back to silence.

---

## Quick start (reflection, no assembly reference)

```csharp
// Resolve once and cache these.
var broker = AccessTools.TypeByName("LocalTts.LocalTtsBroker"); // or Type.GetType across loaded asms
var request = broker?.GetMethod("RequestSpeech", new[] { typeof(string) });
var getStatus = broker?.GetMethod("GetStatus", new[] { typeof(string) });
var getPath = broker?.GetMethod("GetResultPath", new[] { typeof(string) });

// Fire a request — returns a ticket id immediately (null if Local TTS is unavailable/disabled).
string ticket = (string)request?.Invoke(null, new object[] { "The caravan has arrived." });

// Later (e.g. on a tick), poll:
int status = ticket != null ? (int)getStatus.Invoke(null, new object[] { ticket }) : 2;
if (status == 1) // READY
{
    string wavPath = (string)getPath.Invoke(null, new object[] { ticket });
    // play wavPath yourself, or hand it back to the broker's player — see PlaySpeech.
}
```

If you'd rather take a hard reference on `LocalTts.dll`, every call below is a direct static call
and you additionally get `OnComplete` (a main-thread callback).

---

## API reference

### Status constants

| Constant | Value | Meaning |
|---|---|---|
| `STATUS_PENDING` | `0` | Synthesis queued or running. |
| `STATUS_READY` | `1` | The WAV is fully written and playable at the result path. |
| `STATUS_FAILED` | `2` | Synthesis failed (also returned for an unknown/expired ticket). |

Readiness is **strict**: a ticket flips to `READY` only after the file is completely written, so
`READY` is a safe "the audio exists now" gate.

### `string RequestSpeech(string text)`
Simplest form — default voice, normal speed and volume.
**Returns** a ticket id, or `null` if Local TTS is unavailable, disabled, or `text` is blank.

### `string RequestSpeech(string text, string voice, float speed, float volume)`
Request with an explicit voice and parameters.
- `voice` — see **Voice parameter** below.
- `speed` — speaking-rate multiplier; `1.0` = normal. Values `≤ 0` are treated as `1.0`.
- `volume` — output gain; `1.0` = unchanged. Values `≤ 0` are treated as `1.0`.

### `string RequestSpeech(string text, string voice, string blendVoice, float blendAmount, float speed, float volume)`
Full control, including a two-voice blend.
- `blendVoice` — a second voice (same forms as `voice`) mixed into the first.
- `blendAmount` — `0.0` = pure `voice`, `1.0` = pure `blendVoice` (linear style-vector interpolation).

### `void PlaySpeech(string text)` / `void PlaySpeech(string text, string voice, float speed, float volume)`
Fire-and-forget: synthesize and play through Local TTS's **own** audio player. No ticket returned.
Use this when you just want a line spoken and don't need the file.

### `int GetStatus(string ticketId)`
Current status of a ticket (`0`/`1`/`2`). Returns `STATUS_FAILED` for an unknown ticket.

### `string GetResultPath(string ticketId)`
The staged WAV path once the ticket is `READY`; otherwise `null`.

### `string GetError(string ticketId)`
The failure reason when a ticket is `FAILED`; otherwise `null`. Failure always carries a reason —
silence is never the contract.

### `bool OnComplete(string ticketId, Action<string,string> callback)`
Register a completion callback. Fires as `(ticketId, path)` — `path` is the staged WAV on success,
`null` on failure (then check `GetError`). **Always invoked on the main thread**; if the ticket is
already finished it fires on the next frame. Returns `false` for an unknown ticket.
*Direct-reference callers only* — reflection callers should poll `GetStatus`/`GetResultPath`, which
carries no delegate-marshaling concern.

---

## The voice parameter

`voice` and `blendVoice` are a single flexible string:

| Form | Example | Meaning |
|---|---|---|
| empty / `null` | `""` | The mod's configured default voice. |
| bundled voice id | `"af_heart"` | One of the 29 bundled Kokoro voices (see the Player's Guide for the list). |
| filesystem path | `@"C:\...\myvoice.bin"` | A caller-supplied Kokoro `[510,256]` float32 style-vector `.bin` (your own designed voice, or an external export). |

## Behavior you can rely on

- **Asynchronous.** Synthesis runs on Local TTS's worker thread — never yours, never the main thread.
- **Cached.** The output is keyed by a hash of `text + voice + blendVoice + blendAmount + speed + volume`.
  An identical request is an instant cache hit and never re-synthesizes. Concurrent duplicate requests
  dedupe onto the same in-flight synthesis.
- **Staged output.** 16-bit PCM WAV, mono, 24 kHz, written atomically (you never observe a half-written file).
- **Bounded.** The WAV cache is evicted LRU past a configurable size cap (the mod's *Cache size cap*
  setting); in-memory ticket metadata is likewise bounded, so a long-lived ticket id may eventually
  expire to `FAILED`. Read the result promptly, or re-request (a cache hit is cheap).
- **Safe when absent/disabled.** Every entry point returns `null` (or no-ops for `PlaySpeech`) when
  the mod is disabled or not yet initialized — your reflection path degrades to silence cleanly.

## Threading summary

| Concern | Where it runs |
|---|---|
| `RequestSpeech` / `PlaySpeech` call | Returns immediately on your calling thread. |
| Synthesis | Local TTS worker thread. |
| `OnComplete` callback | Main thread (next frame if already done). |
| `GetStatus` / `GetResultPath` / `GetError` | Safe to poll from any thread. |

---

## Building from source

The Kokoro model and native libraries are **not** stored in git — run `download-assets.ps1` once
after cloning to populate `Resources/`, then build `Source/LocalTts.csproj`. See the repository
README for details. The [Voice Pipeline](Voice_Pipeline) page explains what happens between your
`RequestSpeech` call and the staged WAV.
