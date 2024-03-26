A couple of reminders and important things to remembers:

1 - All the type templates (the one in DefaultTypes) are used only to get the fragments. The code around are just to preserve
    the correct indentation.

2 - If any indentation change in GhostComponentSerializer.cs or CommandDataSerializer.cs please check that the corresponding indentantion
    level in the type template match

3 - THIS IS VERY IMPORTANT: all generated classes (so the ones in CommandDataSerializer, GhostComponentSerializer, GhostComponentRegistrationSytem and RpcCommandSerializer)
    MUST BE ANNOTATED WITH [System.Runtime.CompilerServices.CompilerGenerated].


