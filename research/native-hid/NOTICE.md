# Sources and attribution

This is an independent project. GameSir did not author or approve these modifications.

The native packet decoder follows the public [SDL GameSir HID driver](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_gamesir.c). That source was used to identify the header, axes, buttons and activation request.

The UFW container and cipher work follows [xsyetopz/gamesir-fw](https://github.com/xsyetopz/gamesir-fw), whose relevant format implementation was published as public-domain code. The offline decoder identifies that source in its header. No vendor updater from that project was run during this investigation.

The chip-family references were the [JL703N SDK](https://gitlab.zh-jieli.com/JL-Studio/SoundBox/703N/JL703N_SoundBox_1.0.0) and its pi32v2 / BR27 toolchain and ROM symbols. This isn't GameSir source code. Vendor compiler binaries, SDK archives, DLLs, firmware packages and complete disassemblies are not redistributed here.

[GameSir's manual](https://gamesir.com/support/manuals/gamesir-g7-pro-aimlabs-edition) supplied the documented connection and recovery procedures. The hardware observations and exact target checks are recorded separately.

The code and documents written for PandaPro8K use the repository's MIT license. That license does not grant rights to GameSir's firmware, branding or vendor tools. The distributed XOR manifests contain the changes needed to transform user-supplied, hash-checked packages; they are not complete firmware images.
