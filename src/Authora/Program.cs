// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Data;
using Authora.Features.Users.Create;
using Authora.Features.Users.Delete;
using Authora.Features.Users.Get;
using Authora.Features.Users.GetAll;
using Authora.Features.Users.Update;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddDbContext<AuthoraContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database"));
});

builder.Services.AddTransient<CreateUserService>();
builder.Services.AddTransient<GetUserService>();
builder.Services.AddTransient<GetAllUsersService>();
builder.Services.AddTransient<UpdateUserService>();
builder.Services.AddTransient<DeleteUserService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.UseHttpsRedirection();

app.Run();
