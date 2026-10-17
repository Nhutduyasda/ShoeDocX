using UniversalDocDecoder.Protocol;
using UniversalDocDecoder.Worker;

// No native decoder is initialized and no document bytes are read until OS confinement is verified.
try
{
    SandboxGuard.Enter();
    using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
    await Wire.Write(output, new Message(1, "ready", Limits.Build));
    var request = await Wire.Read<DecodeRequest>(input); Wire.Validate(request);
    var data = new byte[request.ByteLength]; await input.ReadExactlyAsync(data);
    Limits.Require(input.ReadByte() == -1, "IPC_TRAILING_INPUT"); Wire.Hash(request, data);
    await Decoder.Decode(request, data, output);
    return 0;
}
catch (Exception e) when (e is not AccessViolationException)
{
    var code = e is ProtocolFault ? e.Message : "DECODE_FAILED";
    try { await Wire.Write(Console.OpenStandardOutput(), new Message(1, "failed", Limits.Build, Code: code)); } catch { }
    return 2;
}
