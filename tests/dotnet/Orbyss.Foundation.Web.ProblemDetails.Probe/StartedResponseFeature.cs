using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
internal sealed class StartedResponseFeature : IHttpResponseFeature
{
    public int StatusCode { get; set; } = 200;
    public string? ReasonPhrase { get; set; }
    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
    public Stream Body { get; set; } = new MemoryStream();
    public bool HasStarted => true;
    public void OnStarting(Func<object, Task> callback, object state) { }
    public void OnCompleted(Func<object, Task> callback, object state) { }
}
