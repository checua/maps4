using maps4.Models;
using maps4.Repositorios;
using maps4.Repositorios.Contrato;
using maps4.Repositorios.Implementacion;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();


builder.Services.AddScoped<IGenericRepository<TipoPropiedad>, TipoPropiedadRepository>();
builder.Services.AddScoped<IGenericRepository<Usuario>, UsuarioRepositorio>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
