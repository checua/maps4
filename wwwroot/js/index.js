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
                            scaledSize: new google.maps.Size(32, 32),
                            origin: new google.maps.Point(0, 0), // Origen de la imagen (0, 0)
                            anchor: new google.maps.Point(16, 16) // Punto de anclaje de la imagen (centrado)
                        },
                    });

                    markerx.customInfo = item.precio;

                    markersx[i] = markerx;
                    i++;

                    markerx.addListener('click', function () {

                        //document.getElementById("hfidInmueble").value = val.idInmueble;

                        //var str = String(document.getElementById("hfCorreo").value);
                        //var res = str.toUpperCase();


                        //var str2 = String(val.correo);
                        //var res2 = str2.toUpperCase();

                        //alert(res + " -  " + res2);

                        //if (res == res2) {
                        //    $("#btnEliminar").show();
                        //}
                        //else {
                        //    $('#btnEliminar').css('display', "none");
                        //}

                        GetCode1(item.idTipo, item.idInmueble, item.nombreCompleto, item.telefono, item.terreno, item.construccion, item.precio, item.observaciones, item.contacto, item.imagenes);

                    });


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

                                var location1 = results[0].geometry.location;
                                var lat1 = latLng.lat().toFixed(6);
                                var lng1 = latLng.lng().toFixed(6);
                                var adress1 = results[0].formatted_address;

                                marker = new google.maps.Marker({
                                    position: latLng,
                                    map: map
                                });

                                markers[i] = marker;
                                i++;

                                var label = document.getElementById('ubicacion');
                                label.textContent = "(" + lat1 + "," + lng1 + ")";

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

                    if ($("#cboTipoPropiedad option").length > 1) {
                        $("#tipo").append(
                            $("<option>").val(item.idTipoPropiedad).text(item.nombre)
                        );
                    }

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

function numberWithCommas(x) {
    if (typeof x !== 'number' && typeof x !== 'string') {
        return x;  // Devuelve el valor sin cambios si no es un número o una cadena
    }
    var parts = x.toString().split('.');
    parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return parts.join('.');
}

function panel(imgElement) {
    // Mostrar el modal de imagen completa
    $('#modalImagenCompleta').modal('show');
}

function GetCode1(a, b, c, d, e, f, g, h, i, j) {
    $('#tipo').val(a);
    $('#terreno').val(numberWithCommas(e));
    $('#construccion').val(numberWithCommas(f));
    $('#precio').val("$ " + numberWithCommas(g));
    $('#descripcion').val(h);
    $('#contacto_a').val(i);
    document.getElementById("ubicacion").innerHTML = j;

    $('#imgViewer').remove();
    $('#carouselInner').empty(); // Limpiar el carrusel antes de añadir nuevas imágenes

    $('div#test1').append('<div id="imgViewer" class="slider"></div>');
    if (i > 0) {
        for (var x = 1; x <= j; x++) {
            var imgSrc = `Cargas/${b}_${x}.jpg`;
            var imgElement = `
                <div id="images${x}" class="item">
                    <img src="${imgSrc}" onclick="panel(this);" id="_${b}_${x}" style="margin-left:2px; max-height:50px !important;">
                </div>
            `;
            $('#imgViewer').append(imgElement);

            // Añadir la imagen al carrusel
            var carouselItem = `
                <div class="carousel-item ${x === 1 ? 'active' : ''}">
                    <img src="${imgSrc}" class="d-block w-100" style="max-height:500px; margin:auto;">
                </div>
            `;
            $('#carouselInner').append(carouselItem);
        }
    } else {
        $('#imgViewer').append($('<img>', { src: "images/nouser.jpg", width: '50px', height: '50px' }));
    }
    $("#modalInmueble").modal("show");
}

function GetCode2() {

    //$("#txtNombreCompleto").val(_modeloEmpleado.nombreCompleto);
    //$("#cboDepartamento").val(_modeloEmpleado.idDepartamento == 0 ? $("#cboDepartamento option:first").val() : _modeloEmpleado.idDepartamento)
    //$("#txtSueldo").val(_modeloEmpleado.sueldo);
    //$("#txtFechaContrato").val(_modeloEmpleado.fechaContrato)

    $("#modalInmueble").modal("show");

}
function ShowImagePreview(evt) {
    var files = evt.files;
    if (files.length) {
        var filePromises = [];
        for (var i = 0, f; f = files[i]; i++) {
            filePromises.push(resizeImageFile(f));
        }

        Promise.all(filePromises).then(function (resizedFiles) {
            // Crear un nuevo DataTransfer para actualizar el input file
            var dataTransfer = new DataTransfer();
            resizedFiles.forEach(function (file) {
                dataTransfer.items.add(file);
            });

            // Reemplazar los archivos en el input file con los archivos redimensionados
            evt.files = dataTransfer.files;

            // Mostrar miniaturas
            for (var file of resizedFiles) {
                var reader = new FileReader();
                reader.onload = function (e) {
                    var imgElement = document.createElement("img");
                    imgElement.src = e.target.result;
                    imgElement.style.height = "100px";  // Estilo opcional para fijar altura
                    imgElement.style.marginBottom = "2px";  // Margen inferior opcional
                    imgElement.style.marginRight = "2px";  // Margen derecho opcional
                    imgElement.style.display = "inline-block";  // Mostrar como bloque en línea
                    document.getElementById('imgViewer').appendChild(imgElement);
                };
                reader.readAsDataURL(file);
            }
        });
    } else {
        alert("Failed to load files");
    }
}

async function resizeImageFile(file) {
    return new Promise(function (resolve) {
        var reader = new FileReader();
        reader.onload = function (e) {
            var img = new Image();
            img.onload = function () {
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

                // Convertir data URI a Blob y luego a File
                fetch(dataUri).then(res => res.blob()).then(function (blob) {
                    var resizedFile = new File([blob], file.name, { type: 'image/jpeg' });
                    resolve(resizedFile);
                });
            };
            img.src = e.target.result;
        };
        reader.readAsDataURL(file);
    });
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


function clearPreviewAndFields() {
    document.getElementById('tipo').value = '';
    document.getElementById('terreno').value = '';
    document.getElementById('construccion').value = '';
    document.getElementById('imgViewer').innerHTML = '';
}

$(document).on("click", ".boton-guardar-inmueble", function () {
    const formData = new FormData();

    const ubicacionTexto = document.getElementById('ubicacion').textContent;
    const { lat, lng } = extractLatLon(ubicacionTexto);

    // Agrega datos del modelo al FormData
    formData.append('Datax.Lat', lat.toFixed(6));  // Asegúrate de que tenga 6 decimales
    formData.append('Datax.Lng', lng.toFixed(6));  // Asegúrate de que tenga 6 decimales
    formData.append('Datax.IdTipo', $("#tipo").val());
    formData.append('Datax.Terreno', $("#terreno").val());
    formData.append('Datax.Construccion', $("#construccion").val());
    formData.append('Datax.Precio', $("#precio").val());
    formData.append('Datax.Observaciones', $("#descripcion").val());
    formData.append('Datax.Contacto', $("#contacto_a").val());

    // Agrega los archivos al FormData
    const files = document.getElementById("FileUpload1").files;
    for (let i = 0; i < files.length; i++) {
        formData.append('Files', files[i]);
    }

    formData.append('Correo', document.getElementById("lnkAcceso").innerText);

    fetch("/Inmueble/RegistrarInmueble", {
        method: 'POST',
        body: formData  // Nota que no establecemos Content-Type. FormData lo hará automáticamente.
    })
        .then(response => response.json())
        .then(response => {
            if (response.success) {
                //alert(response.message);
                location.reload();
            } else {
                alert("Error al guardar el inmueble y las imágenes");
            }
        })
        .catch(error => {
            console.error('Error al enviar datos:', error);
            alert("Error en la red o servidor");
        });
});

// Función para extraer latitud y longitud del texto del label
function extractLatLon(text) {
    const parts = text.match(/\(([\d.-]+),\s*([\d.-]+)\)/);
    if (parts) {
        let lat = parseFloat(parts[1]).toFixed(6);
        let lng = parseFloat(parts[2]).toFixed(6);
        return { lat: parseFloat(lat), lng: parseFloat(lng) };
    }
    return { lat: null, lng: null }; 
}


$(document).on("click", "#cerrarmodalImagenCompleta", function () {
    $("#modalImagenCompleta").modal("hide");
});
