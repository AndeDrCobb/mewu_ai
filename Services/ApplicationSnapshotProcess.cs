// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace mewu_ai_Assistant.Services;

internal static class ApplicationSnapshotProcess
{
    internal const string Argument="--application-snapshot-worker";
    private const int MaximumResponseBytes=8_000_000;

    internal static async Task<ApplicationSnapshotDocument> ReadAsync(ApplicationSnapshotTarget target,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!target.IsCurrent())throw new InvalidOperationException(LocalizationService.T("原窗口已关闭或改变，请重新吸附选择。","The source window changed or closed. Select it again."));
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var ct=deadline.Token;
        var exe=Path.ChangeExtension(typeof(App).Assembly.Location,".exe");
        using var process=new Process{StartInfo=new(exe){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        process.StartInfo.ArgumentList.Add(Argument);
        if(!process.Start())throw new IOException("Cannot start application snapshot reader.");
        var errors=process.StandardError.BaseStream.CopyToAsync(Stream.Null,ct);
        try
        {
            await WriteMessage(process.StandardInput.BaseStream,target,4096,ct);process.StandardInput.Close();
            var result=await ReadMessage<ApplicationSnapshotDocument>(process.StandardOutput.BaseStream,MaximumResponseBytes,ct);
            await process.WaitForExitAsync(ct);await errors;
            ct.ThrowIfCancellationRequested();
            if(process.ExitCode!=0||!target.IsCurrent()||string.IsNullOrWhiteSpace(result.Text)||result.Text.Length>ApplicationSnapshotService.MaximumCharacters||result.Title is null||result.Title.Length>512)
                throw new InvalidDataException("Invalid application snapshot.");
            return result;
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested)
        {throw new TimeoutException(LocalizationService.T("应用未及时提供内容。请稍后重试，或拖选区域使用长截图。","The application did not respond. Retry, or drag a region for scrolling capture."));}
        catch(EndOfStreamException)
        {throw new InvalidDataException(LocalizationService.T("此应用未能提供可读取的完整文本，请拖选区域使用长截图。","This application could not provide readable document text. Drag a region for scrolling capture."));}
        finally
        {
            if(!process.HasExited){try{process.Kill(true);}catch(InvalidOperationException){}await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));}
            try{await errors;}catch(OperationCanceledException){}
        }
    }

    internal static async Task<int> RunWorkerAsync()
    {
        try
        {
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(14));
            using var input=Console.OpenStandardInput();using var output=Console.OpenStandardOutput();
            var target=await ReadMessage<ApplicationSnapshotTarget>(input,4096,deadline.Token);
            var result=await Task.Run(()=>ApplicationSnapshotService.Read(target),deadline.Token);
            await WriteMessage(output,result,MaximumResponseBytes,deadline.Token);return 0;
        }
        catch(Exception){return 1;} // No titles, content or provider error text in logs.
    }

    private static async Task WriteMessage<T>(Stream stream,T value,int limit,CancellationToken token)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            if(bytes.Length>limit)throw new InvalidDataException("Snapshot message exceeds capacity.");
            var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,bytes.Length);
            await stream.WriteAsync(header,token);await stream.WriteAsync(bytes,token);await stream.FlushAsync(token);
        }
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    private static async Task<T> ReadMessage<T>(Stream stream,int limit,CancellationToken token)
    {
        var header=new byte[4];await stream.ReadExactlyAsync(header,token);var length=BinaryPrimitives.ReadInt32LittleEndian(header);
        if(length<1||length>limit)throw new InvalidDataException("Invalid snapshot message size.");
        var bytes=new byte[length];
        try{await stream.ReadExactlyAsync(bytes,token);return JsonSerializer.Deserialize<T>(bytes)??throw new InvalidDataException("Empty snapshot message.");}
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
}
