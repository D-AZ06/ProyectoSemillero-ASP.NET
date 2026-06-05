using MongoDB.Bson;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class ProyectosController : Controller
    {
        private Conexion conexionDB = new Conexion();

        // GET: Proyectos
        // Recibimos los parámetros que vienen del formulario de la vista
        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            try
            {
                // Seguridad básica por sesión
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                // ==========================================
                // NUEVO: Obtener Nombres de los Semilleros
                // ==========================================
                var coleccionSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
                var listaSemilleros = coleccionSemilleros.Find(_ => true).ToList();

                // Convertimos la lista en un Diccionario (Id -> Nombre) para usarlo fácil en la vista
                ViewBag.DiccionarioSemilleros = listaSemilleros.ToDictionary(s => s.IdSemillero, s => s.nombreSemillero);
                // ==========================================

                // Iniciamos el constructor de filtros de MongoDB
                var builder = Builders<DatosProyecto>.Filter;
                FilterDefinition<DatosProyecto> filtroSeguridad;

                // 1. FILTRO DE SEGURIDAD (Obligatorio e invisible para el usuario)
                if (rolUsuario == "Administrador")
                {
                    filtroSeguridad = builder.Empty; // El admin no tiene restricciones, ve todo
                }
                else
                {
                    if (Session["IdSemillero"] != null)
                    {
                        int idSemillero = (int)Session["IdSemillero"];
                        filtroSeguridad = builder.Eq(p => p.IdSemillero, idSemillero); // Solo su semillero
                    }
                    else
                    {
                        TempData["Error"] = "Tu usuario no tiene un semillero asignado correctamente.";
                        return View(new List<DatosProyecto>());
                    }
                }

                // 2. FILTRO DE BÚSQUEDA (Si el usuario envió algo desde el buscador)
                FilterDefinition<DatosProyecto> filtroBusqueda = builder.Empty; // Por defecto vacío

                if (!string.IsNullOrEmpty(tipoFiltro) && !string.IsNullOrEmpty(valorFiltro))
                {
                    switch (tipoFiltro)
                    {
                        case "idProyecto":
                            if (int.TryParse(valorFiltro, out int idProy))
                                filtroBusqueda = builder.Eq(p => p.IdProyecto, idProy);
                            break;

                        case "tituloProyecto":
                            filtroBusqueda = builder.Eq(p => p.TituloProyecto, valorFiltro);
                            break;

                        case "objetivoProyecto":
                            filtroBusqueda = builder.Eq(p => p.ObjetivoProyecto, valorFiltro);
                            break;

                        case "descripcionProyecto":
                            filtroBusqueda = builder.Eq(p => p.DescripcionProyecto, valorFiltro);
                            break;

                        case "fechaInicioProyecto":
                            filtroBusqueda = builder.Eq(p => p.FechaInicioProyecto, valorFiltro);
                            break;

                        case "mesInicioProyecto":
                            // El input type="month" envía el dato como "YYYY-MM" (Ej: "2026-06").
                            // Como en la BD está guardado como "YYYY-MM-DD", le decimos a Mongo: 
                            // "Busca los que EMPIECEN con '2026-06'" usando una Expresión Regular (^ significa "empieza con")
                            filtroBusqueda = builder.Regex(p => p.FechaInicioProyecto, new BsonRegularExpression($"^{valorFiltro}"));
                            break;

                        case "fechaFinProyecto":
                            filtroBusqueda = builder.Eq(p => p.FechaFinProyecto, valorFiltro);
                            break;

                        case "mesFinProyecto":
                            // Aplica la misma lógica de Regex que usamos para el mes de inicio
                            filtroBusqueda = builder.Regex(p => p.FechaFinProyecto, new BsonRegularExpression($"^{valorFiltro}"));
                            break;

                        case "idSemillero":
                            if (int.TryParse(valorFiltro, out int idSem))
                                filtroBusqueda = builder.Eq(p => p.IdSemillero, idSem);
                            break;

                        case "nombreSemillero":
                            // 1. Buscamos en la lista de semilleros (que ya habíamos consultado arriba) 
                            // el que coincida con el nombre que el usuario escribió.
                            var semilleroEncontrado = listaSemilleros.FirstOrDefault(s => s.nombreSemillero.Equals(valorFiltro, StringComparison.OrdinalIgnoreCase));

                            if (semilleroEncontrado != null)
                            {
                                // 2. Si lo encuentra, filtramos los proyectos por su ID
                                filtroBusqueda = builder.Eq(p => p.IdSemillero, semilleroEncontrado.IdSemillero);
                            }
                            else
                            {
                                // 3. Si por alguna razón envía un nombre falso, buscamos un ID inexistente (-1) 
                                // para que la tabla regrese vacía correctamente.
                                filtroBusqueda = builder.Eq(p => p.IdSemillero, -1);
                            }
                            break;
                    }
                }

                // 3. UNIÓN DE FILTROS Y EJECUCIÓN
                // Esto es la magia: Junta la regla de rol con lo que el usuario buscó.
                var filtroFinal = builder.And(filtroSeguridad, filtroBusqueda);

                // Ejecutamos la consulta en MongoDB con los filtros combinados
                List<DatosProyecto> listaProyectos = coleccionProyectos.Find(filtroFinal).ToList();

                return View(listaProyectos);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al cargar o filtrar la lista: " + ex.Message;
                return View(new List<DatosProyecto>());
            }
        }
    }
}