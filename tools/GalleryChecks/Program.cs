using System.Net;
using System.Reflection;
using System.Text.Json;
using RoweMod.Core;
foreach (var status in new[]{HttpStatusCode.OK,HttpStatusCode.NotFound,HttpStatusCode.Forbidden}) {
 var handler=new Fake(status); using var http=new HttpClient(handler);
 var method=typeof(GalleryClient).GetMethod("PutFile",BindingFlags.NonPublic|BindingFlags.Static)!;
 var task=(Task)method.Invoke(null,new object[]{http,"owner/repo","submit/test","mods/test-mod/item.json",new byte[]{1,2},"Update",CancellationToken.None})!;
 bool failed=false;try { await task; }catch(InvalidOperationException){failed=true;}
 if(status==HttpStatusCode.Forbidden){if(!failed||handler.Writes!=0)throw new Exception("Error must block upload");}
 else {if(failed||handler.Writes!=1)throw new Exception("Expected upload");using var json=JsonDocument.Parse(handler.Body);bool sha=json.RootElement.TryGetProperty("sha",out var s);if(sha!=(status==HttpStatusCode.OK)||(sha&&s.GetString()!="old-sha"))throw new Exception("Wrong SHA");}
 Console.WriteLine("PASS "+status);
}
sealed class Fake(HttpStatusCode status):HttpMessageHandler {
 public int Writes;public string Body="";
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct){
 if(req.Method==HttpMethod.Get){if(!req.RequestUri!.Query.Contains("ref=submit%2Ftest"))throw new Exception("Missing branch");return new(status){Content=new StringContent("{\"sha\":\"old-sha\"}")};}
 Writes++;Body=await req.Content!.ReadAsStringAsync(ct);return new(HttpStatusCode.Created){Content=new StringContent("{}")};}
}
