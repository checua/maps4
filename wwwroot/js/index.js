var map;
var geocoder;
var infoWindow;
var marker;
var markerx;
var markers = [];
var markersx = [];
var banSiExc = 1;
var banNoExc = 1;
var banCasaVenta = 1;
var banCasaRenta = 1;
var banTerreVenta = 1;
var mousedUp = false;
var degrees = 90;
var img = null;
var canvas = null;

//const _modeloEmpleado = {
//    idEmpleado: 0,
//    nombreCompleto: "",
//    idDepartamento: 0,
//    sueldo: 0,
//    fechaContrato: ""
//}
document.addEventListener("DOMContentLoaded", function () {
    var i = 0;

    var latLng = new google.maps.LatLng(24.02, -104.62);
    var opciones = {
        center: latLng,
        zoom: 12,
        mapTypeId: google.maps.MapTypeId.roadmap,
        disableDefaultUI: true
    };

    var map = new google.maps.Map(document.getElementById('map_canvas'), opciones);
    geocoder = new google.maps.Geocoder();
    infowindow = new google.maps.InfoWindow();


    fetch("/Home/listaInmuebles")
        .then(response => {
            return response.ok ? response.json() : Promise.reject(response)
        })
        .then(responseJson => {

            if (responseJson.length > 0) {

                responseJson.forEach((item) => {
                    switch (item.idTipo) {
                        //case 0:
                        //    imageIcon = "images/icon/casa_che.png";
                        //    break;
                        case 2:
                            imageIcon = "images/icon/casa_che.png";
                            break;
                        case 3:
                            imageIcon = "images/icon/casa.png";
                            break;
                        case 4:
                            imageIcon = "images/icon/apartment1.png";
                            break;
                        case 5:
                            imageIcon = "images/icon/apartment2.png";
                            break;
                        case 6:
                            imageIcon = "images/icon/terreno1b.png";
                            break;
                        case 7:
                            imageIcon = "images/icon/terreno2.png";
                            break
                        case 8:
                            imageIcon = "images/icon/local.png";
                            break
                        case 9:
                            imageIcon = "images/icon/local2.png";
                            break
                        case 10:
                            imageIcon = "images/icon/building.png";
                            break
                        case 11:
                            imageIcon = "images/icon/building2.png";
                            break
                        case 12:
                            imageIcon = "images/icon/montacargas2.png";
                            break
                        case 13:
                            imageIcon = "images/icon/montacargas.png";
                            break
                        case 14:
                            imageIcon = "images/icon/desk.png";
                            break
                        case 15:
                            imageIcon = "images/icon/desk2.png";
                            break
                        case 16:
                            imageIcon = "images/icon/tractor2.png";
                            break
                        case 17:
                            imageIcon = "images/icon/tractor3.png";
                            break
                    }

                    var latLng = new google.maps.LatLng(item.lat, item.lng);
                    markerx = new google.maps.Marker({
                        position: latLng,
                        map,
                        title: String(item.idTipo),
                        icon: {
                            url: imageIcon,
                            scaledSize: new google.maps.Size(32, 32)
                        },
                    });

                    markerx.customInfo = item.precio;

                    markersx[i] = markerx;
                    i++;




                })
                map.setCenter(latLng);
            }
        })

    google.maps.event.addListener(map, 'mousedown', function (e) {
        mousedUp = false;
        setTimeout(function () {

            var latLng = e.latLng;

            if (mousedUp === false) {
            var log = document.getElementById("lnkAcceso").innerText;

                if (log != "Iniciar Sesión") //Le cambie para no batallar en entrar, pero hay que regresar a ==
                    alert("Ingresa para poder registrar propiedades");
                else {
                    //alert(log);
                    geocoder.geocode({ 'latLng': latLng }, function (results, status) {

                        //alert("Geocode");
                        if (status == google.maps.GeocoderStatus.OK) {

                            //alert("Status ok");

                            if (results[0]) {

                                //            document.getElementById("hfCoordenadas").value = results[0].geometry.location;
                                //            document.getElementById("hfLat").value = event.latLng.lat().toFixed(6);
                                //            document.getElementById("hfLng").value = event.latLng.lng().toFixed(6);
                                //            document.getElementById("hfDireccion").value = results[0].formatted_address;

                                var location1 = results[0].geometry.location;
                                var lat1 = latLng.lat().toFixed(6);
                                var lat2 = latLng.lng().toFixed(6);
                                var adress1 = results[0].formatted_address;

                                marker = new google.maps.Marker({
                                    position: latLng,
                                    map: map
                                });

                                markers[i] = marker;
                                i++;

                                //alert("Si result 1");
                                //alert(location1 + " " + lat1 + " " + lat2 + " " + adress1);

                                GetCode2();



                            } else {
                                //            document.getElementById('geocoding').innerHTML =
                                //                'No se encontraron resultados';
                                alert("No result 0");
                            }

                        }
                        else {
                            document.getElementById("lnkAcceso").innerHTML = 'Geocodificación  ha fallado debido a: ' + status;
                        }
                    });
                }

            }
        }, 500);

    });

    google.maps.event.addListener(map, 'mouseup', function (event) {
        mousedUp = true;
    });
    google.maps.event.addListener(map, 'dragstart', function (event) {
        mousedUp = true;
    });
    google.maps.event.addListener(map, 'touchstart', function (event) {
        mousedUp = true;
    });

    $.ajax({
        url: '/Home/GetUserClaims',
        type: 'GET',
        success: function (data) {
            // Maneja los claims devueltos aquí
            //console.log(data);
            $("#lnkAcceso").text(data[0].value);
        },
        error: function () {
            console.error('Error al obtener los claims del usuario.');
        }
    });



    fetch("/Home/listaTipoPropiedades")
        .then(response => {
            return response.ok ? response.json() : Promise.reject(response)
        })
        .then(responseJson => {

            if (responseJson.length > 0) {
                responseJson.forEach((item) => {

                    $("#cboTipoPropiedad").append(
                        $("<option>").val(item.idTipoPropiedad).text(item.nombre)
                    )

                })
            }

        })

    //$("#txtFechaContrato").datepicker({
    //    format: "dd/mm/yyyy",
    //    autoclose: true,
    //    todayHighlight: true
    //})


}, false);



function MostrarModal() {

    //$("#txtNombreCompleto").val(_modeloEmpleado.nombreCompleto);
    //$("#cboDepartamento").val(_modeloEmpleado.idDepartamento == 0 ? $("#cboDepartamento option:first").val() : _modeloEmpleado.idDepartamento)
    //$("#txtSueldo").val(_modeloEmpleado.sueldo);
    //$("#txtFechaContrato").val(_modeloEmpleado.fechaContrato)


    $("#modalEmpleado").modal("show");

}

$(document).on("click", ".boton-nuevo-acceso", function () {

    //_modeloEmpleado.idEmpleado = 0;
    //_modeloEmpleado.nombreCompleto = "";
    //_modeloEmpleado.idDepartamento = 0;
    //_modeloEmpleado.sueldo = 0;
    //_modeloEmpleado.fechaContrato = "";

    MostrarModal();

})

$(document).on("click", ".boton-iniciar-sesion", function () {
    fetch("/Inicio/IniciarSesion?correo=" + $("#correo").val() + "&contra=" + $("#contra").val())
        .then(response => {
            return response.ok ? response.json() : Promise.reject(response)
        })
        .then(responseJson => {
            if (responseJson.length > 0) {
                responseJson.forEach((item) => {
                    $("#modalEmpleado").modal("hide");
                    $("#lnkAcceso").text(item.correo);
                    //Swal.fire("Listo! " + item.correo, "Usuario logegado", "success");
                })
            }
            else {
                Swal.fire("Error!", "Usuario no encontrado", "danger");
            }
        })
})


//function GetCode2() {
//    $("#modalDatos").modal("show");
//}
function GetCode2() {

    //$("#txtNombreCompleto").val(_modeloEmpleado.nombreCompleto);
    //$("#cboDepartamento").val(_modeloEmpleado.idDepartamento == 0 ? $("#cboDepartamento option:first").val() : _modeloEmpleado.idDepartamento)
    //$("#txtSueldo").val(_modeloEmpleado.sueldo);
    //$("#txtFechaContrato").val(_modeloEmpleado.fechaContrato)


    $("#modalInmueble").modal("show");

}

//function ShowImagePreview(evt) {
//    var files = evt.files;
//    if (files.length) {
//        for (var i = 0, f; f = files[i]; i++) {
//            var r = new FileReader();
//            r.onload = (function (f) {
//                return function (e) {
//                    var img = new Image();
//                    img.onload = function () {
//                        // Crear un canvas para redimensionar la imagen
//                        var canvas = document.createElement('canvas');
//                        var ctx = canvas.getContext('2d');
//                        var maxWidth = 100;  // Ajustar tamaño de miniatura
//                        var maxHeight = 100; // Ajustar tamaño de miniatura
//                        var width = img.width;
//                        var height = img.height;

//                        // Calcular las nuevas dimensiones manteniendo la relación de aspecto
//                        if (width > height) {
//                            if (width > maxWidth) {
//                                height = Math.round(height * maxWidth / width);
//                                width = maxWidth;
//                            }
//                        } else {
//                            if (height > maxHeight) {
//                                width = Math.round(width * maxHeight / height);
//                                height = maxHeight;
//                            }
//                        }

//                        canvas.width = width;
//                        canvas.height = height;
//                        ctx.drawImage(img, 0, 0, width, height);

//                        var dataUri = canvas.toDataURL('image/jpeg', 0.7);  // Calidad ajustada a 0.7

//                        var imgElement = document.createElement("img");
//                        imgElement.src = dataUri;
//                        imgElement.style.height = "100px";
//                        imgElement.style.marginBottom = "2px";
//                        imgElement.style.marginRight = "2px";
//                        imgElement.style.display = "inline-block";
//                        imgElement.onclick = function () {
//                            fixExifOrientation(this);
//                        };

//                        // Agregar imagen al contenedor de vista previa
//                        document.getElementById('imgViewer').appendChild(imgElement);

//                        // Mostrar el botón "Limpiar"
//                        document.getElementById('btnClear').style.display = 'inline-block';
//                    };
//                    img.src = e.target.result;
//                };
//            })(f);

//            r.readAsDataURL(f);
//        }
//    } else {
//        alert("Failed to load files");
//    }
//}

async function UploadImage(imageData, idInmueble) {
    const formData = new FormData();
    formData.append('file', imageData, `inmueble_${idInmueble}.jpg`);

    try {
        const response = await fetch('/Home/UploadImage', {
            method: 'POST',
            body: formData
        });

        if (response.ok) {
            console.log('Imagen subida correctamente.');
            // Aquí puedes manejar la respuesta del servidor si es necesario
        } else {
            console.error('Error al subir la imagen al servidor.');
        }
    } catch (error) {
        console.error('Error en la solicitud de subida de imagen:', error);
    }
}

function ShowImagePreview(evt) {
    var files = evt.files;
    if (files.length) {
        for (var i = 0, f; f = files[i]; i++) {
            var r = new FileReader();
            r.onload = async function (e) {
                var img = new Image();
                img.onload = async function () {
                    // Mostrar la imagen en miniatura
                    var imgElement = document.createElement("img");
                    imgElement.src = e.target.result;
                    imgElement.style.height = "100px";  // Estilo opcional para fijar altura
                    imgElement.style.marginBottom = "2px";  // Margen inferior opcional
                    imgElement.style.marginRight = "2px";  // Margen derecho opcional
                    imgElement.style.display = "inline-block";  // Mostrar como bloque en línea

                    // Agregar la imagen al contenedor de vista previa
                    document.getElementById('imgViewer').appendChild(imgElement);

                    // Redimensionar y subir la imagen al servidor
                    var maxWidth = 1000;  // Tamaño máximo deseado
                    var maxHeight = 1000;
                    var width = img.width;
                    var height = img.height;

                    // Redimensionar la imagen si es necesario
                    if (width > maxWidth || height > maxHeight) {
                        if (width > height) {
                            height *= maxWidth / width;
                            width = maxWidth;
                        } else {
                            width *= maxHeight / height;
                            height = maxHeight;
                        }
                    }

                    // Crear un canvas para redimensionar la imagen
                    var canvas = document.createElement('canvas');
                    canvas.width = width;
                    canvas.height = height;
                    var ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);

                    // Obtener el data URI de la imagen redimensionada
                    var dataUri = canvas.toDataURL('image/jpeg', 0.7);  // Calidad ajustada a 0.7

                    // Convertir data URI a Blob para enviarlo al servidor
                    var blob = await fetch(dataUri).then(res => res.blob());

                    // Obtener el idInmueble desde donde corresponda en tu aplicación
                    var idInmueble = obtenerIdInmueble(); // Debes implementar esta función

                    // Subir la imagen al servidor
                    await UploadImage(blob, idInmueble);
                };
                img.src = e.target.result;
            };
            r.readAsDataURL(f);
        }
    } else {
        alert("Failed to load files");
    }
}



function clearPreviewAndFields() {
    // Limpiar imágenes del contenedor de vista previa
    document.getElementById('imgViewer').innerHTML = '';

    // Limpiar campos de entrada
    document.getElementById('tipo').value = '';
    document.getElementById('terreno').value = '';
    document.getElementById('construccion').value = '';

    // Ocultar el botón "Limpiar"
    document.getElementById('btnClear').style.display = 'none';
}

// Limpia el modal cuando se cierra
document.getElementById('modalInmueble').addEventListener('hidden.bs.modal', function () {
    clearPreviewAndFields();
});


function clearAll() {
    markers.forEach(function (marker) {
        marker.setMap(null); 
    });

    markers = [];

    // Opcional: También podrías limpiar cualquier otro estado o realizar otras acciones necesarias
}

document.getElementById('modalInmueble').addEventListener('hidden.bs.modal', function () {
    clearAll();
});

$(document).on("click", ".boton-guardar-inmueble", function () {
    //fetch("/Inicio/GuardarInmueble?tipo=" + $("#tipo").val() + "&terreno=" + $("#terreno").val() + "&construccion=" + $("#construccion").val()) 
    //    .then(response => {
    //        return response.ok ? response.json() : Promise.reject(response)
    //    })
    //    .then(responseJson => {
    //        if (responseJson.length > 0) {
    //            responseJson.forEach((item) => {
    //                $("#modalEmpleado").modal("hide");
    //                $("#lnkAcceso").text(item.correo);
    //                //Swal.fire("Listo! " + item.correo, "Usuario logegado", "success");
    //            })
    //        }
    //        else {
    //            Swal.fire("Error!", "Usuario no encontrado", "danger");
    //        }
    //    })
})