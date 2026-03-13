namespace MeowAutoChrome.Contracts;

public interface IHostContextAware
{
    IHostContext? HostContext { get; set; }
}
