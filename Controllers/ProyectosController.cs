using MongoDB.Bson;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

                var todosLosProyectos = coleccionProyectos.Find(new MongoDB.Bson.BsonDocument()).ToList();
                foreach (var p in todosLosProyectos)
                {
                    string estadoCalculado = CalcularEstadoLogico(p.FechaInicioProyecto, p.FechaFinProyecto, p.Estado);

                    // Si la fecha dictamina que el estado cambió, lo actualizamos en la BD silenciosamente
                    if (estadoCalculado != p.Estado)
                    {
                        var update = Builders<DatosProyecto>.Update.Set(x => x.Estado, estadoCalculado);
                        coleccionProyectos.UpdateOne(x => x.IdProyecto == p.IdProyecto, update);
                    }
                }

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

                        case "estadoProyecto":
                            filtroBusqueda = builder.Eq(p => p.Estado, valorFiltro);
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

        private string CalcularEstadoLogico(string fechaInicioStr, string fechaFinStr, string estadoActual)
        {
            // Si el usuario lo canceló, lo pausó o lo finalizó manualmente, bloqueamos el cambio automático
            if (estadoActual == "Cancelado" || estadoActual == "Finalizado" || estadoActual == "Pausado")
            {
                return estadoActual;
            }

            if (DateTime.TryParse(fechaInicioStr, out DateTime fechaInicio) &&
                DateTime.TryParse(fechaFinStr, out DateTime fechaFin))
            {
                DateTime hoy = DateTime.Today;

                if (hoy < fechaInicio)
                    return "Planeado";
                else if (hoy >= fechaInicio && hoy <= fechaFin)
                    return "En ejecución";
                else if (hoy > fechaFin)
                    return "Finalizado";
            }

            return estadoActual ?? "Planeado";
        }

        // GET: Proyectos/Agregar
        [HttpGet]
        public ActionResult Agregar()
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para agregar proyectos.";
                    return RedirectToAction("Index");
                }

                // Pasar el rol a la vista
                ViewBag.RolUsuario = rolUsuario;

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var ultimoProyecto = coleccionProyectos.Find(new MongoDB.Bson.BsonDocument())
                                                      .SortByDescending(p => p.IdProyecto)
                                                      .FirstOrDefault();
                int correlativo = 1;

                if (ultimoProyecto != null)
                {
                    string ultimoIdStr = ultimoProyecto.IdProyecto.ToString();
                    if (ultimoIdStr.StartsWith("30"))
                    {
                        string numeroStr = ultimoIdStr.Substring(2);
                        if (int.TryParse(numeroStr, out int numero))
                        {
                            correlativo = numero + 1;
                        }
                    }
                }

                // Instanciamos el modelo con el ID predictivo y el estado por defecto
                var nuevoProyecto = new DatosProyecto
                {
                    IdProyecto = int.Parse("30" + correlativo.ToString()),
                    Estado = "Planeado"
                };
                // =================================================================

                if (rolUsuario == "Líder")
                {
                    int idSemillero = (int)Session["IdSemillero"];
                    ViewBag.IdSemilleroLider = idSemillero;

                    if (Session["NombreSemillero"] != null)
                    {
                        ViewBag.NombreSemilleroLider = Session["NombreSemillero"].ToString();
                    }
                    else
                    {
                        var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                        var filtro = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("idSemillero", idSemillero);
                        var semilleroDB = coleccionSemilleros.Find(filtro).FirstOrDefault();

                        if (semilleroDB != null && semilleroDB.Contains("nombreSemillero"))
                        {
                            string nombreReal = semilleroDB["nombreSemillero"].AsString;
                            ViewBag.NombreSemilleroLider = nombreReal;
                            Session["NombreSemillero"] = nombreReal;
                        }
                        else
                        {
                            ViewBag.NombreSemilleroLider = "Semillero no encontrado";
                        }
                    }
                }
                else if (rolUsuario == "Admin" || rolUsuario == "Administrador")
                {
                    var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                    var semillerosBD = coleccionSemilleros.Find(new MongoDB.Bson.BsonDocument()).ToList();

                    var listaSemilleros = semillerosBD.Select(s => new
                    {
                        IdSemillero = s["idSemillero"].AsInt32,
                        NombreSemillero = s["nombreSemillero"].AsString
                    }).ToList();

                    ViewBag.ListaSemilleros = new SelectList(listaSemilleros, "IdSemillero", "NombreSemillero");
                }

                // RETORNAMOS LA VISTA PASANDO EL MODELO GENERADO
                return View(nuevoProyecto);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al intentar abrir el formulario: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Proyectos/Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosProyecto nuevoProyecto)
        {
            string rolUsuario = Session["Rol"]?.ToString();

            try
            {
                if (string.IsNullOrEmpty(rolUsuario)) return RedirectToAction("IniciarSesion", "Home");

                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para agregar proyectos.";
                    return RedirectToAction("Index");
                }

                ModelState.Remove("IdProyecto");

                if (ModelState.IsValid)
                {
                    // Dentro de [HttpPost] Agregar, justo debajo de: if (ModelState.IsValid) {
                    if (DateTime.TryParse(nuevoProyecto.FechaInicioProyecto, out DateTime fInicio) &&
                        DateTime.TryParse(nuevoProyecto.FechaFinProyecto, out DateTime fFin))
                    {
                        // Validamos que la fecha fin sea de al menos un mes adelante
                        if (fFin < fInicio.AddMonths(1))
                        {
                            TempData["Error"] = "Validación fallida: La fecha de finalización debe ser de al menos un mes posterior a la fecha de inicio.";

                            // Recargar los ViewBags necesarios para volver a pintar la vista
                            ViewBag.RolUsuario = rolUsuario;
                            if (rolUsuario == "Líder")
                            {
                                ViewBag.NombreSemilleroLider = Session["NombreSemillero"];
                                ViewBag.IdSemilleroLider = Session["IdSemillero"];
                            }
                            else if (rolUsuario == "Admin" || rolUsuario == "Administrador")
                            {
                                var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                                var listaSemilleros = coleccionSemilleros.Find(new MongoDB.Bson.BsonDocument()).ToList().Select(s => new
                                {
                                    IdSemillero = s["idSemillero"].AsInt32,
                                    NombreSemillero = s["nombreSemillero"].AsString
                                }).ToList();
                                ViewBag.ListaSemilleros = new SelectList(listaSemilleros, "IdSemillero", "NombreSemillero");
                            }

                            return View(nuevoProyecto);
                        }
                    }


                    var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                    var proyectoDuplicado = coleccionProyectos.Find(p =>
                        p.TituloProyecto.ToLower() == nuevoProyecto.TituloProyecto.ToLower() &&
                        p.IdSemillero == nuevoProyecto.IdSemillero
                        ).FirstOrDefault();

                    if (proyectoDuplicado != null)
                    {
                        TempData["Error"] = "Ya existe un proyecto registrado con este mismo título en tu semillero.";
                        // Recargar ViewBags correspondientes...
                        return View(nuevoProyecto);
                    }

                    // --- AQUÍ EMPIEZA LA LÓGICA DEL ID GENERATIVO AUTOMÁTICO ---
                    var ultimoProyecto = coleccionProyectos.Find(new MongoDB.Bson.BsonDocument())
                                                         .SortByDescending(p => p.IdProyecto)
                                                         .FirstOrDefault();
                    int correlativo = 1;

                    if (ultimoProyecto != null)
                    {
                        string ultimoIdStr = ultimoProyecto.IdProyecto.ToString();
                        if (ultimoIdStr.StartsWith("30"))
                        {
                            string numeroStr = ultimoIdStr.Substring(2);
                            if (int.TryParse(numeroStr, out int numero))
                            {
                                correlativo = numero + 1;
                            }
                        }
                    }

                    nuevoProyecto.IdProyecto = int.Parse("30" + correlativo.ToString());
                    // --- AQUÍ TERMINA LA LÓGICA DEL ID GENERATIVO ---

                    // Asignación del Semillero para el Líder
                    if (rolUsuario == "Líder")
                    {
                        nuevoProyecto.IdSemillero = (int)Session["IdSemillero"];
                    }

                    // Inicializar las listas vacías 
                    nuevoProyecto.Actividades = new List<Actividad>();

                    // Calculamos el estado inicial en base a las fechas que el usuario ingresó
                    nuevoProyecto.Estado = CalcularEstadoLogico(nuevoProyecto.FechaInicioProyecto, nuevoProyecto.FechaFinProyecto, "Planeado");

                    // Guardar en MongoDB
                    coleccionProyectos.InsertOne(nuevoProyecto);

                    TempData["Exito"] = $"El proyecto '{nuevoProyecto.TituloProyecto}' ha sido registrado con el ID: {nuevoProyecto.IdProyecto}";
                    return RedirectToAction("Agregar", "Actividades", new { idProyecto = nuevoProyecto.IdProyecto, flujoSecuencial = true });
                }

                // Si hay error en el formulario, recargamos ViewBag
                ViewBag.RolUsuario = rolUsuario;
                if (rolUsuario == "Líder")
                {
                    ViewBag.NombreSemilleroLider = Session["NombreSemillero"];
                    ViewBag.IdSemilleroLider = Session["IdSemillero"];
                }
                else if (rolUsuario == "Admin" || rolUsuario == "Administrador")
                {
                    var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                    var listaSemilleros = coleccionSemilleros.Find(new MongoDB.Bson.BsonDocument()).ToList().Select(s => new
                    {
                        IdSemillero = s["idSemillero"].AsInt32,
                        NombreSemillero = s["nombreSemillero"].AsString
                    }).ToList();
                    ViewBag.ListaSemilleros = new SelectList(listaSemilleros, "IdSemillero", "NombreSemillero");
                }

                TempData["Error"] = "Por favor, revisa los campos del formulario. Hay datos inválidos.";
                return View(nuevoProyecto);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error inesperado al guardar el proyecto: " + ex.Message;
                ViewBag.RolUsuario = rolUsuario;
                return View(nuevoProyecto);
            }
        }

        // GET: Proyectos/Modificar
        [HttpGet]
        public ActionResult Modificar(int id)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para modificar proyectos.";
                    return RedirectToAction("Index");
                }

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var proyecto = coleccionProyectos.Find(p => p.IdProyecto == id).FirstOrDefault();

                if (proyecto == null)
                {
                    TempData["Error"] = "No se encontró el proyecto solicitado.";
                    return RedirectToAction("Index");
                }

                if (rolUsuario == "Líder")
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    if (proyecto.IdSemillero != idSemilleroLider)
                    {
                        TempData["Error"] = "Acceso denegado: Este proyecto pertenece a otro semillero.";
                        return RedirectToAction("Index");
                    }
                }

                // LÓGICA SIMPLIFICADA: Buscamos el nombre del Semillero para mostrarlo como solo lectura (Para Admin y Líder)
                var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                var filtroSemillero = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("idSemillero", proyecto.IdSemillero);
                var semilleroDB = coleccionSemilleros.Find(filtroSemillero).FirstOrDefault();

                if (semilleroDB != null && semilleroDB.Contains("nombreSemillero"))
                {
                    ViewBag.NombreSemillero = semilleroDB["nombreSemillero"].AsString;
                }
                else
                {
                    ViewBag.NombreSemillero = "Semillero Desconocido";
                }

                ViewBag.RolUsuario = rolUsuario;
                return View(proyecto);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al procesar la solicitud: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Proyectos/Modificar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosProyecto proyectoModificado)
        {
            string rolUsuario = Session["Rol"]?.ToString();

            try
            {
                if (string.IsNullOrEmpty(rolUsuario)) return RedirectToAction("IniciarSesion", "Home");

                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para modificar proyectos.";
                    return RedirectToAction("Index");
                }

                if (ModelState.IsValid)
                {
                    if (DateTime.TryParse(proyectoModificado.FechaInicioProyecto, out DateTime fInicio) &&
                        DateTime.TryParse(proyectoModificado.FechaFinProyecto, out DateTime fFin))
                    {
                        if (fFin < fInicio.AddMonths(1))
                        {
                            TempData["Error"] = "Validación fallida: El rango de duración del proyecto modificado no puede ser inferior a un mes.";

                            // Recargamos el rol del usuario para la vista
                            ViewBag.RolUsuario = rolUsuario;

                            // Buscamos el nombre del semillero original para volver a mostrarlo como solo lectura
                            var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                            var filtroSemillero = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("idSemillero", proyectoModificado.IdSemillero);
                            var semilleroDB = coleccionSemilleros.Find(filtroSemillero).FirstOrDefault();

                            ViewBag.NombreSemillero = semilleroDB != null && semilleroDB.Contains("nombreSemillero")
                                ? semilleroDB["nombreSemillero"].AsString
                                : "Semillero Desconocido";

                            // Devolvemos el modelo a la vista con la alerta sin guardar nada en MongoDB
                            return View(proyectoModificado);
                        }
                    }

                    var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                    var filtro = Builders<DatosProyecto>.Filter.Eq(p => p.IdProyecto, proyectoModificado.IdProyecto);

                    // Validamos que el Líder no intente enviar un ID de proyecto falso en el HTML para editar proyectos ajenos
                    if (rolUsuario == "Líder")
                    {
                        int idSemilleroLider = (int)Session["IdSemillero"];
                        filtro = Builders<DatosProyecto>.Filter.And(
                            Builders<DatosProyecto>.Filter.Eq(p => p.IdProyecto, proyectoModificado.IdProyecto),
                            Builders<DatosProyecto>.Filter.Eq(p => p.IdSemillero, idSemilleroLider)
                        );
                    }

                    var proyectoOriginal = coleccionProyectos.Find(filtro).FirstOrDefault();

                    if (proyectoOriginal != null)
                    {
                        // CUIDADO DE DATOS VITALES:
                        proyectoModificado.Id = proyectoOriginal.Id;
                        proyectoModificado.Actividades = proyectoOriginal.Actividades ?? new List<Actividad>();
                        proyectoModificado.IdSemillero = proyectoOriginal.IdSemillero;


                        proyectoModificado.TituloProyecto = proyectoModificado.TituloProyecto?.Trim();

                        var proyectoDuplicado = coleccionProyectos.Find(p =>
                            p.TituloProyecto.ToLower() == proyectoModificado.TituloProyecto.ToLower() &&
                            p.IdSemillero == proyectoOriginal.IdSemillero &&
                            p.IdProyecto != proyectoModificado.IdProyecto // EXCLUSIÓN: Ignora a sí mismo
                        ).FirstOrDefault();

                        if (proyectoDuplicado != null)
                        {
                            TempData["Error"] = "No se pueden guardar los cambios: Ya existe otro proyecto diferente registrado con este mismo título en el semillero.";

                            // Recargar los datos obligatorios para pintar la vista de nuevo sin errores
                            ViewBag.RolUsuario = rolUsuario;

                            var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                            var filtroSemillero = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("idSemillero", proyectoModificado.IdSemillero);
                            var semilleroDB = coleccionSemilleros.Find(filtroSemillero).FirstOrDefault();
                            ViewBag.NombreSemillero = semilleroDB != null && semilleroDB.Contains("nombreSemillero")
                                ? semilleroDB["nombreSemillero"].AsString
                                : "Semillero Desconocido";

                            return View(proyectoModificado);
                        }

                        if (proyectoModificado.Estado == "Reanudar")
                        {
                            // Si pidió reanudar, forzamos que calcule el estado normal según las fechas
                            proyectoModificado.Estado = CalcularEstadoLogico(proyectoModificado.FechaInicioProyecto, proyectoModificado.FechaFinProyecto, "Planeado");
                        }
                        else if (proyectoModificado.Estado != "Cancelado" && proyectoModificado.Estado != "Finalizado" && proyectoModificado.Estado != "Pausado")
                        {
                            // Si no es un estado forzado manual, recalculamos por si modificó las fechas de inicio/fin
                            proyectoModificado.Estado = CalcularEstadoLogico(proyectoModificado.FechaInicioProyecto, proyectoModificado.FechaFinProyecto, proyectoOriginal.Estado);
                        }

                        var resultado = coleccionProyectos.ReplaceOne(filtro, proyectoModificado);

                        if (resultado.MatchedCount > 0)
                        {
                            TempData["Exito"] = "La información del proyecto se ha actualizado correctamente.";
                        }
                        else
                        {
                            TempData["Error"] = "No se pudo actualizar el proyecto.";
                        }
                    }
                    else
                    {
                        TempData["Error"] = "No se encontró el proyecto a modificar o no tienes permisos.";
                    }

                    return RedirectToAction("Index");
                }

                ViewBag.RolUsuario = rolUsuario;
                TempData["Error"] = "Por favor, revisa los campos del formulario.";
                return View(proyectoModificado);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error inesperado al modificar: " + ex.Message;
                ViewBag.RolUsuario = rolUsuario;
                return View(proyectoModificado);
            }
        }

        // GET: Proyectos/Eliminar
        public ActionResult Eliminar(int id)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                // 1. Restricción para Investigador
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para eliminar proyectos.";
                    return RedirectToAction("Index");
                }

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                MongoDB.Driver.DeleteResult resultado;

                // 2. Lógica de eliminación según el rol
                if (rolUsuario == "Administrador" || rolUsuario == "Admin")
                {
                    // El administrador puede borrar cualquier proyecto buscando solo por su ID
                    resultado = coleccionProyectos.DeleteOne(p => p.IdProyecto == id);
                }
                else if (rolUsuario == "Líder")
                {
                    // El líder solo puede borrar si coincide el ID del proyecto Y que pertenezca a su semillero
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    resultado = coleccionProyectos.DeleteOne(p => p.IdProyecto == id && p.IdSemillero == idSemilleroLider);
                }
                else
                {
                    // Por si acaso existe algún otro rol no contemplado
                    TempData["Error"] = "Rol no reconocido para esta acción.";
                    return RedirectToAction("Index");
                }

                // 3. Validación del resultado
                if (resultado.DeletedCount > 0)
                {
                    TempData["Exito"] = "El proyecto ha sido eliminado permanentemente del sistema.";
                }
                else
                {
                    TempData["Error"] = "No se pudo eliminar. Puede que el proyecto ya no exista o pertenezca a otro semillero.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al intentar eliminar el proyecto: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}