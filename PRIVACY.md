# Privacy

VoiceLab performs DSP and file operations locally. It has no account system, telemetry, analytics, cloud API, update downloader, advertising SDK, or network feature.

Microphone samples are held in bounded memory buffers while the engine runs. Raw microphone audio is not written by the application. Recording is an explicit user action and writes processed output to the chosen local folder. Logs contain timestamps, technical status, and exception text; they do not contain audio samples.

Settings, presets, logs, and recordings remain in the paths documented in README.md. Users control those files through normal Windows file permissions and can remove them without contacting a service.
