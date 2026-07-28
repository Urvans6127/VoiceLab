# VoiceLab manual hardware-validation checklist

Use a normal, non-administrator Windows 10 or 11 x64 account. Record the Windows build, microphone name, driver version, endpoint format, and result for each check.

## Startup and interface

- [ ] Launch `VoiceLab.exe` without administrator privileges.
- [ ] Confirm no installer, driver prompt, firewall prompt, account, or download appears.
- [ ] Confirm Recording is the initial page and there are no third-party routing controls; local playback appears only in the Presets-page Live Preview card.
- [ ] Confirm all pages render correctly at 100%, 125%, and 150% display scaling.
- [ ] Switch English to Turkish and back; confirm every visible label updates immediately.
- [ ] Restart and confirm the selected language and page persist.
- [ ] Open the language list and confirm its popup width matches the ComboBox.

## Microphone and DSP

- [ ] Grant Windows microphone permission, refresh devices, and select a microphone.
- [ ] Test 48 kHz and 44.1 kHz with Safe, Balanced, and Low Latency.
- [ ] Confirm input and processed meters respond during recording.
- [ ] Test input gain, noise gate, pitch, tone controls, robot, echo, reverb, and output gain.
- [ ] Confirm each changed effect is audible in the saved recording.
- [ ] Select each built-in preset and confirm it changes the recorded result.
- [ ] Save, reload, import, export, rename, and delete eligible custom presets.
- [ ] Disconnect and reconnect the microphone; confirm clear recovery or error behavior.
- [ ] Test device-busy, access-denied, and unsupported-format cases where available.

## Local Live Preview

- [ ] Select a microphone and a physical playback device, preferably headphones.
- [ ] Start Live Preview and confirm the processed signal is audible without creating a WAV file.
- [ ] Change presets and custom DSP controls; confirm the preview changes without restarting.
- [ ] Stop and restart preview repeatedly; confirm the microphone and playback device are released each time.
- [ ] Disconnect the active preview device and confirm preview stops safely.
- [ ] Close VoiceLab during preview and confirm no process remains.

## Recording

- [ ] Start recording and verify the WAV contains the processed microphone signal.
- [ ] Pause and resume; confirm no silence is inserted and paused time is excluded.
- [ ] Stop normally and verify the WAV header, playback, duration, sample rate, and channel count.
- [ ] Confirm no local playback occurs during any recording state.
- [ ] Test an unwritable or full destination and confirm a clear warning.
- [ ] Close the window during recording and confirm bounded finalization and process shutdown.

## Persistence and shutdown

- [ ] Confirm microphone, preset, recording folder, quality, language, page, and window state persist.
- [ ] Confirm a normal window close leaves no process, audio session, timer, or locked file.
- [ ] Confirm Windows sign-out or shutdown during recording completes without deadlock or repeated dialogs.
- [ ] Trigger a controlled unexpected UI exception in a development build and verify one message, logging, bounded cleanup, and termination.

## Privacy and release contents

- [ ] Monitor the process during normal use and confirm no network connections or DNS requests.
- [ ] Confirm logs contain technical messages only and no audio samples or secrets.
- [ ] Confirm settings, presets, logs, and recordings stay in the documented local paths.
- [ ] Before publishing the source repository, confirm no binaries, symbols, archives, logs, recordings, user settings, Python environments, model files, or experiments are tracked.
