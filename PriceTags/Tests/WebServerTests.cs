using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using FanShop.PriceTags.Models;
using FanShop.PriceTags.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FanShop.PriceTags.Tests;
public sealed class WebServerTests
{
    [Fact]
    public async Task RealHttpServerServesAssetsAndProcessesScansIdempotently()
    {
        var directory=Path.Combine(Path.GetTempPath(),"fanshop-http-"+Guid.NewGuid());
        var storage=new LocalStorage(directory); var catalog=new ProductCatalogService(); catalog.Replace([new("00001","ЦБ-1","Кружка","MISC")]);
        var session=new ScanSessionService(catalog,storage,NullLogger<ScanSessionService>.Instance);
        await using var server=new BarcodeWebServer(session,catalog,NullLogger<BarcodeWebServer>.Instance);
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        try
        {
            await server.RestartAsync(port); using var client=new HttpClient {BaseAddress=new Uri($"http://localhost:{port}")};
            Assert.Contains("Готов к сканированию",await client.GetStringAsync("/"));
            Assert.Contains("getRandomValues",await client.GetStringAsync("/app.js")); Assert.Contains("font-family",await client.GetStringAsync("/styles.css"));
            var request=new ScanRequest(" 00001 ",Guid.NewGuid());
            var response=await client.PostAsJsonAsync("/api/scan",request);response.EnsureSuccessStatusCode();
            var result=await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(result.GetProperty("found").GetBoolean()); Assert.Equal("00001",result.GetProperty("barcode").GetString());
            (await client.PostAsJsonAsync("/api/scan",request)).EnsureSuccessStatusCode(); Assert.Single(session.Snapshot());
            var unknown=await client.PostAsJsonAsync("/api/scan",new ScanRequest("999",Guid.NewGuid()));unknown.EnsureSuccessStatusCode(); Assert.False((await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("found").GetBoolean());
            Assert.Equal(2,session.Snapshot().Length); Assert.Equal(ScanSource.Network,session.Snapshot()[0].Source);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/scan",new ScanRequest("",Guid.NewGuid()))).StatusCode);
            session.SetFrozen(true); Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/scan",new ScanRequest("00001",Guid.NewGuid()))).StatusCode);
            using var crossOrigin=new HttpRequestMessage(HttpMethod.Post,"/api/scan"){Content=JsonContent.Create(new ScanRequest("00001",Guid.NewGuid()))};crossOrigin.Headers.Add("Origin","https://external.example");Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(crossOrigin)).StatusCode);
            await server.StopAsync(); Assert.False(server.IsRunning);
        }
        finally {await server.StopAsync();storage.Dispose();Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task BusyPortProducesActionableError()
    {
        var directory=Path.Combine(Path.GetTempPath(),"fanshop-http-"+Guid.NewGuid());
        var storage=new LocalStorage(directory);var catalog=new ProductCatalogService();var session=new ScanSessionService(catalog,storage,NullLogger<ScanSessionService>.Instance);
        await using var server=new BarcodeWebServer(session,catalog,NullLogger<BarcodeWebServer>.Instance);
        var listener=new TcpListener(IPAddress.Any,0); listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;
        try {var error=await Assert.ThrowsAsync<IOException>(()=>server.RestartAsync(port));Assert.Contains("порт",error.Message);Assert.False(server.IsRunning);}
        finally {listener.Stop(); storage.Dispose(); Directory.Delete(directory,true);}
    }
}
