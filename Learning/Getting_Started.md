# Getting Started

## Requirements

- **Windows 64-bit** (RimWorld 1.6). No other mod is required — Local TTS is standalone.
- Optional: **RimSynapse Core** v0.9.0+. When present, Local TTS loads after it and reports its
  GPU memory footprint to Core's GPU stats channel; when absent, nothing is lost but that.
- **~350 MB free disk space** for the bundled Kokoro model and voices.
- Optional: any **DirectX 12 GPU** (NVIDIA, AMD, or Intel). Synthesis uses the GPU via DirectML
  when one is present and falls back to multi-threaded CPU inference otherwise — no configuration
  needed either way.

## Installation

Subscribe on the Steam Workshop (or download a GitHub release) and enable the mod. On first load
the mod initialises the speech engine in the background; the game remains fully responsive while
it does.

## Mod settings

Open **Options → Mod options → RimSynapse - Local Text-to-Speech**:

| Setting | What it does |
|---|---|
| **Acceleration** | Auto (recommended), GPU, or CPU. Auto probes DirectML and silently falls back to CPU. |
| **Voice** | Pick any of the 29 bundled English voices (US and UK, male and female). |
| **Blend with / amount** | Mix a second voice into the first for a custom timbre — Kokoro's style vectors interpolate cleanly. |
| **Speed** | Playback rate of synthesis (not a pitch shift — Kokoro re-times naturally). |
| **Volume** | Output gain applied at playback. |
| **Cache size cap** | Maximum disk the synthesized-audio cache may use (MB). Oldest lines are evicted first (LRU) once the cap is reached. Default 250 MB. |
| **Test line** | An open text box with Speak/Stop buttons — audition any sentence with the current settings. |

## The bundled voices

Local TTS ships **29 English voices**. Kokoro's naming is `<language><gender>_<name>` — the first
letter is the accent, the second is the gender:

| Family | Accent & gender | Count | Examples |
|---|---|---|---|
| `af_*` | US English, female | 12 | `af_heart` (default), `af_bella`, `af_nicole`, `af_sky` |
| `am_*` | US English, male | 9 | `am_michael`, `am_adam`, `am_puck` |
| `bf_*` | British English, female | 4 | `bf_emma`, `bf_alice`, `bf_lily` |
| `bm_*` | British English, male | 4 | `bm_george`, `bm_daniel`, `bm_fable` |

Pick one as your **Voice**, and optionally **Blend** a second into it for a timbre that sits between
them. Each voice carries its own accent for pronunciation (US voices phonemize as `en-us`, British
as `en-gb`), so the accent is consistent from spelling through to sound.

## Verifying it works

With **Dev mode** on, open the Debug Actions menu (wrench icon) → **RimSynapse** category:

- **LocalTTS: Synthesize + dump stats (Log)** — runs the whole pipeline headlessly and logs sample
  count, duration, peak amplitude, and the active backend. No audio device needed.
- **LocalTTS: Speak test line** — the full path including playback.
- **LocalTTS: Dump engine status (Log)** — model, backend, vocabulary, and settings snapshot.

## Troubleshooting

- **No sound but stats look right:** check RimWorld's master/sound volume and the mod's Volume
  slider; the synthesis log line proves the engine itself is healthy.
- **First line is slow:** the model loads lazily on first use (a few seconds, once per session).
  Subsequent lines are fast.
- **GPU not used:** Auto mode requires a DirectX 12 capable GPU and Windows 10 1903+. The engine
  status dump shows which backend is active.
