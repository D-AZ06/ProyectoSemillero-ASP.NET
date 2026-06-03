using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace ProyectoSemillero_ASP.NET.Models
{
    public class Conexion
    {
        private readonly IMongoDatabase _database;

        public Conexion()
        {
            string conexion = ConfigurationManager
                .ConnectionStrings["MongoDB"]
                .ConnectionString;

            var cliente = new MongoClient(conexion);
            _database = cliente.GetDatabase("BD-ProSemillero");
        }

        public IMongoDatabase Database => _database;
    }
}