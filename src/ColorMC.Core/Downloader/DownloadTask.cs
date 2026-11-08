using ColorMC.Core.GuiHandle;
using ColorMC.Core.Objs;

namespace ColorMC.Core.Downloader;

/// <remarks>
/// 下载任务
/// </remarks>
/// <param name="gui">GUI下载参数</param>
internal class DownloadTask(IDownloadGui? gui, IProgressGui? pgui, CancellationToken token)
{
    /// <summary>
    /// 任务是否已经取消
    /// </summary>
    /// <returns></returns>
    internal CancellationToken Token => _cancel.Token;

    /// <summary>
    /// 取消下载
    /// </summary>
    private readonly CancellationTokenSource _cancel = CancellationTokenSource.CreateLinkedTokenSource(token);

    /// <summary>
    /// 总下载数量
    /// </summary>
    private int _allSize;
    /// <summary>
    /// 已下载数量
    /// </summary>
    private int _doneSize;
    /// <summary>
    /// 下次错误数量
    /// </summary>
    private int _errorSize;

    /// <summary>
    /// 处理完成信号
    /// </summary>
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _completed;

    /// <summary>
    /// 设置任务数量
    /// </summary>
    public void SetSize(int size)
    {
        _allSize = size;
        gui?.UpdateTask(UpdateType.AddItems, _allSize);
    }

    /// <summary>
    /// 等待下载完成
    /// </summary>
    /// <returns>是否成功下载</returns>
    public Task<bool> WaitDone()
    {
        if (_allSize == 0)
            Complete(false);
        else if (Token.IsCancellationRequested)
            Complete(false);
        else
            Token.Register(static state => ((DownloadTask)state!).Complete(false), this);
        return _completion.Task;
    }

    /// <summary>
    /// 取消下载
    /// </summary>
    public void Cancel()
    {
        _cancel.Cancel();
        Complete(false);
    }

    /// <summary>
    /// 下载线程完成
    /// </summary>
    public void Done()
    {
        Interlocked.Increment(ref _doneSize);
        gui?.UpdateTask(UpdateType.ItemDone, 1);
        pgui?.SetNowProcess(_doneSize + _errorSize, _allSize);

        ItemDone();
    }

    /// <summary>
    /// 任务下载错误
    /// </summary>
    public void Error()
    {
        Interlocked.Increment(ref _errorSize);
        gui?.UpdateTask(UpdateType.ItemDone, 1);
        pgui?.SetNowProcess(_doneSize + _errorSize, _allSize);

        ItemDone();
    }

    /// <summary>
    /// 线程完成
    /// </summary>
    private void ItemDone()
    {
        if (Volatile.Read(ref _doneSize) + Volatile.Read(ref _errorSize) >= _allSize)
            Complete(Volatile.Read(ref _errorSize) == 0);
    }

    private void Complete(bool succeeded)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;
        _completion.TrySetResult(succeeded && !Token.IsCancellationRequested);
        DownloadManager.TaskDone(gui, this);
    }

    /// <summary>
    /// 更新线程状态
    /// </summary>
    /// <param name="index">线程</param>
    /// <param name="obj">下载文件</param>
    public void UpdateItem(int index, FileItemObj obj)
    {
        gui?.UpdateItem(index, obj);
        if (_allSize == 1 && pgui != null)
        {
            pgui?.SetNowProcess((int)(obj.NowSize / 1000), (int)(obj.AllSize / 1000));
        }
    }
}
