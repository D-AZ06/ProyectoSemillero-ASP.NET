using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoSemillero_ASP.NET.Models
{
    public class DatosFase
    {
        public int IdProyecto { get; set; }
        public string TituloProyecto { get; set; }
        public int IdActividad { get; set; }
        public string NombreActividad { get; set; }

        public int IdFase { get; set; }
        public string NombreFase { get; set; }
        public string DuracionFase { get; set; }
    }
}