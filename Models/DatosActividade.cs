using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoSemillero_ASP.NET.Models
{
    public class DatosActividade
    {
        public int IdProyecto { get; set; }
        public string TituloProyecto { get; set; }
        public int IdActividad { get; set; }
        public string NombreActividad { get; set; }
        public string DuracionActividad { get; set; }
        public string FechaInicioActividad { get; set; }
        public string FechaEntregaActividad { get; set; }
        public string EstadoActividad { get; set; }
        public List<int> InvestigadoresResponsables { get; set; }

        // Campo auxiliar exclusivo para mostrar los nombres en el Index (Frontend)
        public List<string> NombresInvestigadores { get; set; } = new List<string>();
    }
}