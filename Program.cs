using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Repositorios.Implementacion;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Configurar servicios para el contenedor (Dependency Injection)
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Registrar repositorios y servicios (Inyecci�n de dependencias)
builder.Services.AddScoped<IGenericRepository<TipoPropiedad>, TipoPropiedadRepository>();
builder.Services.AddScoped<IGenericRepository<Usuario>, UsuarioRepository>();
builder.Services.AddScoped<IUsuarioServicio<Usuario>, UsuarioRepositoryLogin>();
builder.Services.AddScoped<IGenericRepository<Inmueble>, InmuebleRepository>();
builder.Services.AddScoped<IInmuebleServicio<Inmueble>, InmuebleRegistroRepository>();
builder.Services.AddScoped<IComentarioService, ComentarioService>();

// Configurar autenticaci�n basada en cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(10); // Cambiar a m�s de 10 d�as
        options.SlidingExpiration = true; // Reiniciar el tiempo de expiraci�n en cada solicitud
        options.LoginPath = "/Inicio/IniciarSesion";
        options.Cookie.SameSite = SameSiteMode.Lax; // Ajustar seg�n tu necesidad (Lax, Strict, None)
        options.Cookie.HttpOnly = true; // Solo permite acceso a trav�s de HTTP(S)
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Enviar cookies solo a trav�s de HTTPS
    });

var app = builder.Build();

// Configuraci�n del pipeline de solicitud HTTP
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

// Middleware de seguridad y acceso a archivos est�ticos
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Configurar autenticaci�n y autorizaci�n
app.UseAuthentication();
app.UseAuthorization();

// Configurar rutas para Razor Pages y controladores de MVC
app.MapRazorPages(); // Permitir acceso a Razor Pages
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Iniciar la aplicaci�n
app.Run();




