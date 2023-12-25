using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Experimental.Rendering;

namespace DevkitServer.Core.Cartography.Jobs;

// https://gamedev.stackexchange.com/questions/200761/using-unity-jobs-to-encode-file-to-png-and-then-save-it-on-main-thread
internal struct EncodeImageJob : IJob
{
    public NativeArray<byte> InputTexture;
    public NativeArray<byte> OutputPNG;
    public GraphicsFormat GraphicsFormat;
    public NativeArray<int> OutputSize;
    public Vector2Int Size;
    public bool UseJpeg;
    public int JpegQuality;
    public void Execute()
    {
        NativeArray<byte> actualOutput = UseJpeg
            ? ImageConversion.EncodeNativeArrayToJPG(InputTexture, GraphicsFormat, (uint)Size.x, (uint)Size.y, quality: JpegQuality)
            : ImageConversion.EncodeNativeArrayToPNG(InputTexture, GraphicsFormat, (uint)Size.x, (uint)Size.y);

        NativeArray<byte>.Copy(actualOutput, OutputPNG, actualOutput.Length);

        OutputSize[0] = actualOutput.Length;

        actualOutput.Dispose();
    }
}
