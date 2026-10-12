using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OfficinaTycoon.Web;
using OfficinaTycoon.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton<GameHost>();
await builder.Build().RunAsync();
