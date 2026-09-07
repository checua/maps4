using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Repositorios.Implementacion;
using maps4.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Configurar servicios para el contenedor (Dependency Injection)
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Registrar repositorios y servicios (InyecciÃ³n de dependencias)
builder.Services.AddScoped<IGenericRepository<TipoPropiedad>, TipoPropiedadRepository>();
builder.Services.AddScoped<IGenericRepository<Usuario>, UsuarioRepository>();
builder.Services.AddScoped<IUsuarioServicio<Usuario>, UsuarioRepositoryLogin>();
builder.Services.AddScoped<IGenericRepository<Inmueble>, InmuebleRepository>();
builder.Services.AddScoped<IInmuebleServicio<Inmueble>, InmuebleRegistroRepository>();
builder.Services.AddScoped<IComentarioService, ComentarioService>();
builder.Services.AddScoped<IInventarioRepository, InventarioRepository>();
builder.Services.AddScoped<IRadarMatchingService, RadarMatchingService>();
builder.Services.AddScoped<IRadarAgentPairingRepository, RadarAgentPairingRepository>();
builder.Services.AddScoped<IRadarMessageProcessingRepository, RadarMessageProcessingRepository>();
builder.Services.AddScoped<IRadarMessageDeliveryRepository, RadarMessageDeliveryRepository>();
builder.Services.AddScoped<IRadarPendingDeliveryRepository, RadarPendingDeliveryRepository>();
builder.Services.AddScoped<IRadarPendingProcessingRepository, RadarPendingProcessingRepository>();
builder.Services.AddScoped<IRadarAgentChatDiscoveryRepository, RadarAgentChatDiscoveryRepository>();
builder.Services.AddScoped<IRadarAgentChatDiscoveryCommandRepository, RadarAgentChatDiscoveryCommandRepository>();
builder.Services.AddSingleton<IRadarCentralIntelligenceService, RadarCentralIntelligenceService>();
builder.Services.AddScoped<IRadarCentralProcessingService, RadarCentralProcessingService>();
builder.Services.AddScoped<IBorradorInmuebleRepository, BorradorInmuebleRepository>();
builder.Services.AddScoped<IPublicacionBorradorRepository, PublicacionBorradorRepository>();
builder.Services.AddScoped<IInmuebleFotoRepository, InmuebleFotoRepository>();
builder.Services.AddScoped<IMarketplaceFiltroRepository, MarketplaceFiltroRepository>();
builder.Services.AddScoped<IZonaRepository, ZonaRepository>();
string imageStorageProvider = builder.Configuration["RSMaps:ImageStorageProvider"]?.Trim() ?? "Local";
if (imageStorageProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IInmuebleFotoStorage, AzureBlobInmuebleFotoStorage>();
}
else if (imageStorageProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IInmuebleFotoStorage, LocalInmuebleFotoStorage>();
}
else
{
    throw new InvalidOperationException($"Proveedor de imagenes RSMaps no soportado: {imageStorageProvider}");
}

// Configurar autenticaciÃ³n basada en cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(10); // Cambiar a mÃ¡s de 10 dÃ­as
        options.SlidingExpiration = true; // Reiniciar el tiempo de expiraciÃ³n en cada solicitud
        options.LoginPath = "/Inicio/IniciarSesion";
        options.Cookie.SameSite = SameSiteMode.Lax; // Ajustar segÃºn tu necesidad (Lax, Strict, None)
        options.Cookie.HttpOnly = true; // Solo permite acceso a travÃ©s de HTTP(S)
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Enviar cookies solo a travÃ©s de HTTPS
    });

var app = builder.Build();

// ConfiguraciÃ³n del pipeline de solicitud HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // Configurar HTTP Strict Transport Security (HSTS)
}
else
{
    app.UseDeveloperExceptionPage();
}

// Middleware de seguridad y acceso a archivos estÃ¡ticos
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Configurar autenticaciÃ³n y autorizaciÃ³n
app.UseAuthentication();
app.UseAuthorization();

// Configurar rutas para Razor Pages y controladores de MVC/API
app.MapRazorPages(); // Permitir acceso a Razor Pages
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Iniciar la aplicaciÃ³n
app.Run();
