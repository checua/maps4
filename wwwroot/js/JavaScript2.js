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

window.onload = function () {

    //$(window).load(function() {

    var input2 = document.getElementById('FileUpload1');
    input2.addEventListener("change", readMultipleFiles, false);

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

    $.ajax({
        type: 'POST',
        url: 'WebServices/WebService1.asmx/getInmJson',
        dataType: 'json',
        contentType: 'application/json; charset=utf-8',
        //data: { 'lat': lat, 'lng' : lng }, //parametros que le mandas al WS
        success: OnSuccessCall,
        error: OnErrorCall
    });

    google.maps.event.addListener(map, 'dblclick', function (event) {
    });


    google.maps.event.addListener(map, 'mousedown', function (event) {
        mousedUp = false;
        setTimeout(function () {
            if (mousedUp === false) {
                var log = document.getElementById("lnkNombre").innerText;

                if (log == "No logged")
                    alert("Ingresa para poder registrar propiedades");
                else {
                    //alert(log);
                    geocoder.geocode({ 'latLng': event.latLng }, function (results, status) {
                        if (status == google.maps.GeocoderStatus.OK) {

                            if (results[0]) {

                                document.getElementById("hfCoordenadas").value = results[0].geometry.location;
                                document.getElementById("hfLat").value = event.latLng.lat().toFixed(6);
                                document.getElementById("hfLng").value = event.latLng.lng().toFixed(6);
                                document.getElementById("hfDireccion").value = results[0].formatted_address;

                                marker = new google.maps.Marker({
                                    position: event.latLng,
                                    map: map
                                });

                                GetCode2();

                            } else {
                                document.getElementById('geocoding').innerHTML =
                                    'No se encontraron resultados';
                            }
                        } else {
                            document.getElementById('geocoding').innerHTML =
                                'Geocodificación  ha fallado debido a: ' + status;
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

    google.maps.event.addListener(infowindow, 'click', function (event) {
        alert("hola");
    });

    function OnSuccessCall(response) {
        var markerData = JSON.parse(response.d);
        var i = 0;
        var a1 = new Array();

        var latLng;

        $.each(markerData.Table, function (key, val) {

            switch (val.idTipo) {
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

            latLng = new google.maps.LatLng(val.lat, val.lng);
            var marker = new google.maps.Marker({
                position: latLng,
                title: String(val.idTipo),
                icon: {
                    url: imageIcon,
                    scaledSize: new google.maps.Size(32, 32)
                },
                map: map
            });



            marker.customInfo = val.precio;

            //marker.customInfo1 = val.link;

            //alert(val.precio);

            markers[i] = marker;
            i++;

            var content = '<div id="iw-container">' +
                              '<div id="iw-title">' + val.nombreTipo + '</div>' +
                              '<div id="iw-laterales">' +
                                    '<div id="iw-lateral-izquierda">' +
                                        //'<a href="' + val.observaciones + '">www.myvistaalegre.com</a><img src="http://maps.marnoto.com/en/5wayscustomizeinfowindow/images/vistalegre.jpg" alt="Imagen">' +
                                        '<a title="fotos" href="' + val.link + '" target="blank"><img src="images/Collage-Casas.jpg" alt="Imágenes"/></a>' +
                                    '</div>' + //iw-lateral-izquierda
                                    '<div id="iw-lateral-derecha">' +
                                        '<p>' + val.observaciones + '</p>' +
                                        '</div>' + //iw-lateral-derecha
                              '</div>' + //iw-laterales
                                    '<div id="iw-abajo">' +
                                        '<div class="iw-subTitle2">Asesor: <b>' + val.nombreCompleto + '</b></div>' +
                                        '<div class="iw-subTitle2">Teléfono: <b>' + val.telefono + '</b></div>' +
                                        '<div class="iw-subTitle2">Terreno: <b>' + val.terreno + '</b>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;Construcción: <b>' + val.construccion + '</b></div>' +
                                        '<div class="iw-subTitle2">Precio: <b>' + val.precio + '</b></div>' +
                                        //'<p>VISTA ALEGRE ATLANTIS, SA<br>3830-292 Ílhavo - Portugal<br>' +
                                        //'<br>Tel. +52 (618) 81800181<br>e-mail: geral@vaa.pt<br><a href="http://www.google.com">www.myvistaalegre.com</a></p>'</div>' +
                                        //'<div class="iw-bottom-gradient"></div>' +
                                   '</div>' + //iw-abajo
                            '</div>'; //iw-container


            var infowindow = new google.maps.InfoWindow({
                content: content, //maxWidth: 350
            });

            marker.addListener('click', function () {

                document.getElementById("hfidInmueble").value = val.idInmueble;

                var str = String(document.getElementById("hfCorreo").value);
                var res = str.toUpperCase();


                var str2 = String(val.correo);
                var res2 = str2.toUpperCase();

                //alert(res + " -  " + res2);

                if (res == res2) {
                    $("#btnEliminar").show();
                }
                else {
                    $('#btnEliminar').css('display', "none");
                }

                GetCode1(val.nombreTipo, val.idInmueble, val.nombreCompleto, val.telefono, val.terreno, val.construccion, val.precio, val.observaciones, val.imagenes);

            });



            google.maps.event.addListener(infowindow, 'domready', function () {


                // Reference to the DIV that wraps the bottom of infowindow
                var iwOuter = $('.gm-style-iw');

                var iwCloseBtn = iwOuter.next();

            });


        });
        map.setCenter(latLng);
    }
    function OnErrorCall(response) {
        alert(response.status + " x " + response.statusText);
    } //fin llamada ajax
}

function addListClickListener() {
    var xy = document.getElementById('supTerreno');
    xy.addEventListener('keyup', function (event) {
        alert("hola");
    });
}
function myClear() {
    document.getElementById("selTipo").value = "0";
    document.getElementById("txtTerreno").value = "";
    document.getElementById("txtConstruccion").value = "";
    document.getElementById("txtPrecio").value = "";
    document.getElementById("txtDescripcion").value = "";
    $('#imgViewer').remove();
}

function myFunction() {
    alert("9");

    //alert(document.getElementById("selTipo").value);

    var button = document.getElementById("Button1").value();
    submitBtn.click();

    //var nick = document.getElementById("<%= GuardarUb.ClientID %>").value;
    //nick.click();
}

//function GetCode() {
//    //alert("059864");
//}

function GetCode1(a, b, c, d, e, f, g, h, i) {
    $('#lblTipo').text(a);
    $('#lblidInmueble').text(b + "_" + 1 + ".jpg");
    $('#lblAsesor').text(c);
    $('#lblTelefono').text(d);
    $('#lblTerreno').text(numberWithCommas(e));
    $('#lblConstruccion').text(numberWithCommas(f));
    $('#lblPrecio').text("$ " + numberWithCommas(g));
    $('#lblDescripcion').text(h);
    $('#hfNumImgs').val(i);


    $('#imgViewer2').remove();

    $('div#test').append('<div id="imgViewer2" class="slider" style="height:50px; margin-bottom:2px;"></div>');
    if (i > 0) {
        for (var x = 1 ; x <= i; x++) {
            $('#imgViewer2').append($('<img>', { src: "Cargas/_" + b + "_" + x + ".jpg", onclick: 'panel(this);', id: '_' + b + '_' + x, style: "margin-left:2px;" }));
            //var text = "<div id=imagesx class=item><img src=\"Cargas/_" + b + "_" + x + ".jpg\"  onclick= \"panel(this);\" id=\"_" + b + "_" + x + "\" style= \"margin-left:2px; max-height:50px !important;\"> </div>";
            //$('#imgViewer2').append(text);
        }
    }
    else {
        $('#imgViewer2').append($('<img>', { src: "images/nouser.jpg", width: '50px', height: '50px' }));
    }



    document.getElementById("btnModal1").click();
}

function GetCode2() {
    document.getElementById("btnModal2").click();
}

function GetCode3() {
    document.getElementById("btnModal").click();
}

function GetCode4(i) {
    //alert("barra" + i);
}


function getImgSize(imgSrc) {
    var newImg = new Image();

    newImg.onload = function () {
        var height = newImg.height;
        var width = newImg.width;
        alert('The image size is ' + width + '*' + height);
    }

    newImg.src = imgSrc; // this must be done AFTER setting onload
}

function panel(x) {

    var medida = $('#imgViewer2').height() + $('#modalHeaderTop').height() + 15;
    $('#background').css('height', $('#modalContent').height() - medida);
    $('#background').css('width', $('#modalContent').width());

    $('#background').css('margin-top', $('#imgViewer2').height() + 15);

    var ratioImg = x.height / x.width;
    var ratioBac = parseFloat($('#background').css('height')) / parseFloat($('#background').css('width'));

    //alert(ratioImg + " - " + ratioBac)
    $('#content').html("<img src='" + $(x).attr("src") + "'>");

    if (ratioImg < ratioBac) {

        var bh = parseFloat($('#background').css("height"));
        var bw = parseFloat($('#background').css("width")); //alert(bh);

        //var top = (bh - medida) / 2;

        $('#content img').css('height', 'auto');
        $('#content img').css('width', bw - 10);

        $('#preview').css('margin-top', '65px');
        $('#preview').css('max-height', 'auto');
        $('#preview').css('max-width', 'auto');

        $('#content').css('height', 'auto');
        $('#content').css('width', 'auto');


    }
    else {
        var bh = parseFloat($('#background').css("height")); //alert(bh);
        var bw = parseFloat($('#background').css("width")); //alert(bh);

        //var top = (bh - medida) / 2;

        $('#content img').css('height', bh - 10);
        $('#content img').css('width', 'auto');

        $('#preview').css('margin-top', '65px');
        $('#preview').css('max-height', 'auto');
        $('#preview').css('max-width', 'auto');

        $('#content').css('height', 'auto');
        $('#content').css('width', 'auto');


    }

    // Mostramos las capas
    $('#preview').fadeIn();
    $('#background').fadeIn();
}

function fixExifOrientation($img) {
    var cadena = $img.id,
    separador = "_"; // un espacio en blanco
    arregloDeSubCadenas = cadena.split(separador);
    var numImage = arregloDeSubCadenas[2];

    //alert($img.name);

    var degrees;
    degrees = $img.style.transform;

    if (degrees == "") {
        degrees = 90;
        $($img).css("transform", "rotate(90deg)");
        document.getElementById("HiddenField" + $img.name).value = 6;
    }
    if (degrees == "rotate(90deg)") {
        degrees = 180;
        $($img).css("transform", "rotate(180deg)");
        document.getElementById("HiddenField" + $img.name).value = 3;
    }
    if (degrees == "rotate(180deg)") {
        degrees = 270;
        $($img).css("transform", "rotate(270deg)");
        document.getElementById("HiddenField" + $img.name).value = 8;
    }
    if (degrees == "rotate(270deg)") {
        degrees = 360;
        $($img).css("transform", "rotate(360deg)");
        document.getElementById("HiddenField" + $img.name).value = 1;
    }
    if (degrees == "rotate(360deg)") {
        degrees = 90;
        $($img).css("transform", "rotate(90deg)");
        document.getElementById("HiddenField" + $img.name).value = 6;
    }
}

function clearAll() {
    marker.setMap(null);
    //alert("limpiando");
    myClear();
}

function readMultipleFiles(evt) {
    var files = evt.target.files;
    var count = 0;
    if (files) {
        $('#imgViewer').remove();
        $('div#test1').append('<div id="imgViewer" style="height:50px; margin-bottom:2px;"></div>');
        $('#child').remove();
        $('div#parent').append('<div id="child" style="height:50px; margin-bottom:2px;"></div>');
        //alert("2");
        for (var i = 0, f; f = files[i]; i++) {
            //alert("3");
            var r = new FileReader();
            r.onload = (function (f) {
                //alert("4");
                return function (e) { // WOOHOO!

                    //alert("5");
                    //var dataUri = e.target.result,
                    //    img = document.createElement("img");

                    //img.src = dataUri;
                    //document.body.appendChild(img);

                    $('#imgViewer').append($('<img>', { src: e.target.result, onclick: 'fixExifOrientation(this);', id: 'preview_image_' + count, name: f.name }));

                    count++;

                    var inputHidden = document.createElement("input");
                    inputHidden.setAttribute("type", "hidden");
                    inputHidden.setAttribute("id", "HiddenField" + f.name);
                    inputHidden.setAttribute("ClientIDMode", "Static");
                    inputHidden.setAttribute("name", "HiddenField" + f.name);
                    document.getElementById("hfx").appendChild(inputHidden);

                };
            })(f);

            r.readAsDataURL(f);
        }
    } else {
        alert("Failed to load files");
    }
}
