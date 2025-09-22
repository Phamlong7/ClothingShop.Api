using ClothingShop.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Bind connection string (?u tiên ENV khi deploy)
var cs = builder.Configuration.GetConnectionString("Default")
         ?? builder.Configuration["DATABASE_URL"]
         ?? throw new Exception("No connection string configured.");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(cs));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS cho frontend t?nh
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()
));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.MapControllers();

app.Run();
