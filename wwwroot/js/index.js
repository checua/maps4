var map;
var geocoder;
var infoWindow;
var marker;
var markers = [];
var banSiExc = 1;
var banNoExc = 1;
var banCasaVenta = 1;
var banCasaRenta = 1;
var banTerreVenta = 1;
var mousedUp = false;
var degrees = 90;
var img = null;
var canvas = null;

const _modeloEmpleado = {
    idEmpleado: 0,
    nombreCompleto: "",
    idDepartamento: 0,
    sueldo: 0,
    fechaContrato: ""
}
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
                        case 0:
                            imageIcon = "images/icon/casa_che.png";
                            break;
                        case 1:
                            imageIcon = "images/icon/casa_che.png";
                            break;
                        case 2:
                            imageIcon = "images/icon/casa.png";
                            break;
                        case 3:
                            imageIcon = "images/icon/apartment1.png";
                            break;
                        case 4:
                            imageIcon = "images/icon/apartment2.png";
                            break;
                        case 5:
                            imageIcon = "images/icon/terreno1b.png";
                            break;
                        case 6:
                            imageIcon = "images/icon/terreno2.png";
                            break
                        case 7:
                            imageIcon = "images/icon/local.png";
                            break
                        case 8:
                            imageIcon = "images/icon/local2.png";
                            break
                        case 9:
                            imageIcon = "images/icon/building.png";
                            break
                        case 10:
                            imageIcon = "images/icon/building2.png";
                            break
                        case 11:
                            imageIcon = "images/icon/montacargas2.png";
                            break
                        case 12:
                            imageIcon = "images/icon/montacargas.png";
                            break
                        case 13:
                            imageIcon = "images/icon/desk.png";
                            break
                        case 14:
                            imageIcon = "images/icon/desk2.png";
                            break
                        case 15:
                            imageIcon = "images/icon/tractor2.png";
                            break
                        case 16:
                            imageIcon = "images/icon/tractor3.png";
                            break
                    }

                    var latLng = new google.maps.LatLng(item.lat, item.lng);
                    new google.maps.Marker({
                        position: latLng,
                        map,
                        title: String(item.idTipo),
                        icon: {
                            url: imageIcon,
                            scaledSize: new google.maps.Size(32, 32)
                        },
                    });
                })
                map.setCenter(latLng);
            }
        })

    google.maps.event.addListener(map, 'mousedown', function (e) {

        var latLng = e.latLng;
        //alert(latLng);

        marker = new google.maps.Marker({
            position: latLng,
            map: map,
            draggable: false
        });

        markers[i] = marker;
        i++;

        if (mousedUp === false) {
            var log = document.getElementById("lnkAcceso").innerText;

            //if (log == "Iniciar Sesión")
            //    alert("Ingresa para poder registrar propiedades");
            //else {
            //    alert("Bienvenido");
            //}


            if (log != "Iniciar Sesión") //Le cambie para no batallar en entrar, pero hay que regresar a ==
                alert("Ingresa para poder registrar propiedades");
            else {
                /*alert(log);*/
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

    });

    //MostrarEmpleados();

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


}, false)



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

//    var i;
//    var files = evt.files.length;
//    var count = 0;
//    //alert(files);

//    if (files) {
//        //alert(files);
//        $('#imgViewer').remove();
//        $('div#test1').append('<div id="imgViewer" style="height:50px; margin-bottom:2px;"></div>');
//        $('#child').remove();
//        $('div#parent').append('<div id="child" style="height:50px; margin-bottom:2px;"></div>');
//        //alert(evt.files[0]);
//        for (var i = 0, f; f = evt.files[i]; i++) {
//            //alert(i);
//            var r = new FileReader();
//            //alert("4");
//            r.onload = (function (f) {
//                //alert("4");
//                return function (e) { // WOOHOO!

//                    var dataUri = e.target.result;
//                    var img = document.createElement("img");

//                    img.src = dataUri;
//                    img.style.height = "50px";  // Ajustar tamaño de miniatura
//                    img.style.marginBottom = "2px";
//                    img.style.marginRight = "2px";
//                    img.onclick = function () {
//                        fixExifOrientation(this);
//                    };

//                    // Agregar imagen al contenedor de vista previa
//                    $('#imgViewer').append(img);

//                    count++;

//                    //alert("5");
//                    //var dataUri = e.target.result,
//                    //    img = document.createElement("img");

//                    //img.src = dataUri;
//                    //document.body.appendChild(img);

//                    //$('#imgViewer').append($('<img>', { src: e.target.result, onclick: 'fixExifOrientation(this);', id: 'preview_image_' + count, name: f.name }));

//                    //count++;

//                    //var inputHidden = document.createElement("input");
//                    //inputHidden.setAttribute("type", "hidden");
//                    //inputHidden.setAttribute("id", "HiddenField" + f.name);
//                    //inputHidden.setAttribute("ClientIDMode", "Static");
//                    //inputHidden.setAttribute("name", "HiddenField" + f.name);
//                    //document.getElementById("hfx").appendChild(inputHidden);

//                };
//            })(f);

//            r.readAsDataURL(f);
//        }
//    } else {
//        alert("Failed to load files");
//    }
//}


//function ShowImagePreview(evt) {
//    var count = document.querySelectorAll('#imgViewer img').length; // Contar imágenes existentes
//    var files = evt.files;

//    if (files.length) {
//        for (var i = 0, f; f = files[i]; i++) {
//            var r = new FileReader();
//            r.onload = (function (f) {
//                return function (e) {
//                    var dataUri = e.target.result;
//                    var img = document.createElement("img");

//                    img.src = dataUri;
//                    img.style.height = "50px";  // Ajustar tamaño de miniatura
//                    img.style.marginBottom = "2px";
//                    img.style.marginRight = "2px";
//                    img.onclick = function () {
//                        fixExifOrientation(this);
//                    };

//                    // Agregar imagen al contenedor de vista previa
//                    document.getElementById('imgViewer').appendChild(img);

//                    count++;
//                };
//            })(f);

//            r.readAsDataURL(f);
//        }
//    } else {
//        alert("Failed to load files");
//    }
//}

function ShowImagePreview(evt) {
    var files = evt.files;
    if (files.length) {
        for (var i = 0, f; f = files[i]; i++) {
            var r = new FileReader();
            r.onload = (function (f) {
                return function (e) {
                    var img = new Image();
                    img.onload = function () {
                        // Crear un canvas para redimensionar la imagen
                        var canvas = document.createElement('canvas');
                        var ctx = canvas.getContext('2d');
                        var maxWidth = 100;  // Ajustar tamaño de miniatura
                        var maxHeight = 100; // Ajustar tamaño de miniatura
                        var width = img.width;
                        var height = img.height;

                        // Calcular las nuevas dimensiones manteniendo la relación de aspecto
                        if (width > height) {
                            if (width > maxWidth) {
                                height = Math.round(height * maxWidth / width);
                                width = maxWidth;
                            }
                        } else {
                            if (height > maxHeight) {
                                width = Math.round(width * maxHeight / height);
                                height = maxHeight;
                            }
                        }

                        canvas.width = width;
                        canvas.height = height;
                        ctx.drawImage(img, 0, 0, width, height);

                        var dataUri = canvas.toDataURL('image/jpeg', 0.7);  // Calidad ajustada a 0.7

                        var imgElement = document.createElement("img");
                        imgElement.src = dataUri;
                        imgElement.style.height = "100px";
                        imgElement.style.marginBottom = "2px";
                        imgElement.style.marginRight = "2px";
                        imgElement.style.display = "inline-block";
                        imgElement.onclick = function () {
                            fixExifOrientation(this);
                        };

                        // Agregar imagen al contenedor de vista previa
                        document.getElementById('imgViewer').appendChild(imgElement);

                        // Mostrar el botón "Limpiar"
                        document.getElementById('btnClear').style.display = 'inline-block';
                    };
                    img.src = e.target.result;
                };
            })(f);

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

    alert(markers.length);
    marker.setMap(null);
    alert("limpiando");
    //myClear();

    //for (let i = 0; i < markers.length; i++) {
    //    markers[i].setMap(map);
    //}

}