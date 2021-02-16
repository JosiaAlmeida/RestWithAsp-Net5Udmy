using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using RestAspeNet5.Business;
using RestAspeNet5.Business.Implementacao;
using RestAspeNet5.Hypermedia.Enricher;
using RestAspeNet5.Hypermedia.Filters;
using RestAspeNet5.Modals.Context;
using RestAspeNet5.Repository.Generic;
using Serilog;

namespace RestAspeNet5
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        //Configurando Environment
        public IWebHostEnvironment Environment { get; }
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
            //Configurando Login de serilog
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //Cors
            services.AddCors(opt=> opt.AddDefaultPolicy(builder=> {
                builder.AllowAnyOrigin()
                .AllowAnyMethod().AllowAnyHeader();
            }));
            services.AddControllers();
            //Injetando Mysql Conex�o
            var connection = Configuration["MySQLConnection:MySQLConnectionString"];
            services.AddDbContext<MySQLContext>(options => options.UseMySql(connection));
            //Configurando Migra��o
            if (Environment.IsDevelopment())
            {
                MigrateDatabase(connection);
            }

            //Formatando para formato Xml
            services.AddMvc(opt =>
            {
                opt.RespectBrowserAcceptHeader = true;

                opt.FormatterMappings.SetMediaTypeMappingForFormat("xml", MediaTypeHeaderValue.Parse("application/xml"));
                opt.FormatterMappings.SetMediaTypeMappingForFormat("json", MediaTypeHeaderValue.Parse("application/json"));
            }).AddXmlSerializerFormatters();

            //Adicionando Hateos
            var filteroptions = new HyperMidiaFilterOptions();
            filteroptions.ContentResponsiveEnricherList.Add(new PersonEnricher());
            filteroptions.ContentResponsiveEnricherList.Add(new BookEnricher());

            services.AddSingleton(filteroptions);
            //Swagger
            services.AddSwaggerGen(Sw=> {
                Sw.SwaggerDoc("v1", new OpenApiInfo {
                    Title="Rest Api from 0 To Azure",
                    Version="v1",
                    Description="Learning .NET in developer course",
                    Contact = new OpenApiContact
                    {
                        Name="Josia Almeida",
                        Url= new Uri("http://github.com/JosiaAlmeida")
                    }
                });
            });
            //Inje��o de dependencias
            
            services.AddApiVersioning();
            //Injetando Services
            //Injetando nossa classe de negocio
            services.AddScoped<IPersonBusiness, PersonImplementationBusiness>();
            services.AddScoped<IBooksBusiness, BookImplementationBusiness>();
            services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                //app.UseSwagger();
                //app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RestAspeNet5 v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();
            //Habilitando cors
            //Depois de Https e routing, e antes de endpoints
            app.UseCors();

            app.UseSwagger();
            //Gera uma pagina Html
            app.UseSwaggerUI(Sw=> {
                Sw.SwaggerEndpoint("/swagger/v1/swagger.json", "Rest Api from 0 To Azure - v1");
                }
            );

            var opt = new RewriteOptions();
            opt.AddRedirect("^$", "swagger");
            app.UseRewriter(opt);

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                //Hateos
                endpoints.MapControllerRoute("Defaultapi", ("{controller=value}/{id?}"));
            });
        }
        private void MigrateDatabase(string connection)
        {
            try
            {
                //Base de dados conex�o
                var evolveConnection = new MySql.Data.MySqlClient.MySqlConnection(connection);
                //Inicializando o Evolve
                var evolve = new Evolve.Evolve(evolveConnection, msg => Log.Information(msg))
                {
                    //Rota para as migrations
                    Locations = new List<string> { "db/migrations", "db/dataset" },
                    IsEraseDisabled = true,
                };
                evolve.Migrate();
            }
            catch (Exception ex)
            {
                Log.Error("Erro na migra��o da base de dados", ex);
                throw;
            }
        }
    }
}
