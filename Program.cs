using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Repositorios.Implementacion;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register repositories and services
builder.Services.AddScoped<IGenericRepository<TipoPropiedad>, TipoPropiedadRepository>();
builder.Services.AddScoped<IGenericRepository<Usuario>, UsuarioRepository>();
builder.Services.AddScoped<IUsuarioServicio<Usuario>, UsuarioRepositoryLogin>();
builder.Services.AddScoped<IGenericRepository<Inmueble>, InmuebleRepository>();
builder.Services.AddScoped<IInmuebleServicio<Inmueble>, InmuebleRegistroRepository>();

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(10); // Cambiar a más de 10 días
        options.SlidingExpiration = true; // Opcional, para reiniciar el tiempo de expiración en cada solicitud
        options.LoginPath = "/Inicio/IniciarSesion";
        options.Cookie.SameSite = SameSiteMode.Lax; // Ajusta según tu necesidad (Lax, Strict, None)
        options.Cookie.HttpOnly = true; // Asegura que las cookies solo se envíen a través de solicitudes HTTP(S)
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Middleware configuration
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Configure custom middleware or services here if needed

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
