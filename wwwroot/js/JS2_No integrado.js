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

        //var log = document.getElementById("lnkNombre").innerText;

        //if (log == "No logged")
        //    alert("Ingresa para poder registrar propiedades");
        //else {
        //    //alert(log);
        //    geocoder.geocode({ 'latLng': event.latLng }, function (results, status) {
        //        if (status == google.maps.GeocoderStatus.OK) {



        //            if (results[0]) {

        //                document.getElementById("hfCoordenadas").value = results[0].geometry.location;
        //                document.getElementById("hfLat").value = event.latLng.lat().toFixed(6);
        //                document.getElementById("hfLng").value = event.latLng.lng().toFixed(6);
        //                document.getElementById("hfDireccion").value = results[0].formatted_address;

        //                marker = new google.maps.Marker({
        //                    position: event.latLng,
        //                    map: map
        //                });


        //                //infowindow.setContent(

        //                //    );


        //                //infowindow.open(map, marker);
        //                GetCode2();
        //                //}

        //            } else {
        //                document.getElementById('geocoding').innerHTML =
        //                    'No se encontraron resultados';
        //            }
        //        } else {
        //            document.getElementById('geocoding').innerHTML =
        //                'Geocodificación  ha fallado debido a: ' + status;
        //        }
        //    });
        //}


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

                                //infowindow.setContent(
                                //    );
                                //infowindow.open(map, marker);
                                GetCode2();
                                //}

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

        //alert(response.status + " x " + response.statusText);

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

                alert("info");

                //document.getElementById("hfidInmueble").value = val.idInmueble;

                ////infowindow.open(map, marker);
                ////$('#myModal').modal('show');

                ////alert(document.getElementById("hfCorreo").value + " " + val.correo); // Para ver si son iguales los correos

                //var str = String(document.getElementById("hfCorreo").value);
                //var res = str.toUpperCase();


                //var str2 = String(val.correo);
                //var res2 = str2.toUpperCase();

                ////alert(res + " -  " + res2);

                //if (res == res2) {
                //    //alert("visible");
                //    $("#btnEliminar").show();
                //    //$('#btnEliminar').css('display', 'normal');
                //    //$("#btnEliminar").prop("visible", "true");
                //    //$('#btnEliminar').css('display', 'normal');
                //    //$('#divBtnEliminar').append($('<button>', { id: "btnEliminar", OnClick: 'btnEliminararInmueble_Click', runat: "server", text: "Eliminar" }));
                //}
                //else {
                //    //alert("no     ---    visible");
                //    //$("#btnEliminar").css('display', 'none');
                //    //$("#btnEliminar").hide();
                //    $('#btnEliminar').css('display', "none");
                //}

                //GetCode1(val.nombreTipo, val.idInmueble, val.nombreCompleto, val.telefono, val.terreno, val.construccion, val.precio, val.observaciones, val.imagenes);

            });



            google.maps.event.addListener(infowindow, 'domready', function () {


                // Reference to the DIV that wraps the bottom of infowindow
                var iwOuter = $('.gm-style-iw');

                //    /* Since this div is in a position prior to .gm-div style-iw.
                //     * We use jQuery and create a iwBackground variable,
                //     * and took advantage of the existing reference .gm-style-iw for the previous div with .prev().
                //    */
                //var iwBackground = iwOuter.prev();

                //    // Removes background shadow DIV
                //    iwBackground.children(':nth-child(2)').css({ 'display': 'none' });

                //    // Removes white background DIV
                //    iwBackground.children(':nth-child(4)').css({ 'display': 'none' });

                //    // Moves the infowindow 115px to the right.
                //    iwOuter.parent().parent().css({ left: '0px' });

                //    // Moves the shadow of the arrow 76px to the left margin.
                //    iwBackground.children(':nth-child(1)').attr('style', function (i, s) { return s + 'left: 76px !important;' });

                //    // Moves the arrow 76px to the left margin.
                //    iwBackground.children(':nth-child(3)').attr('style', function (i, s) { return s + 'left: 76px !important;' });

                //    // Changes the desired tail shadow color.
                //    iwBackground.children(':nth-child(3)').find('div').children().css({ 'box-shadow': 'rgba(72, 181, 233, 0.6) 0px 1px 6px', 'z-index': '1' });

                //    // Reference to the div that groups the close button elements.
                var iwCloseBtn = iwOuter.next();

                //    // Apply the desired effect to the close button
                //iwCloseBtn.css({ opacity: '1', right: '0px', top: '0px', border: '5px solid #48b5e9', 'border-radius': '13px', 'box-shadow': '0 0 3px #3990B9' });
                //iwCloseBtn.css({ 'display': 'none' });

                //    // If the content of infowindow not exceed the set maximum height, then the gradient is removed.
                //    if ($('.iw-content').height() < 140) {
                //        $('.iw-bottom-gradient').css({ display: 'none' });
                //    }

                //    // .iw-content sea del mismo tamaño del DIV que lo contiene para que no se vea feo
                //    $('.iw-content').css({ width: '330px' });

                //    // The API automatically applies 0.7 opacity to the button after the mouseout event. This function reverses this event to the desired value.
                //    iwCloseBtn.mouseout(function () {
                //        $(this).css({ opacity: '1' });
                //    });
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
    //alert("myClear");
    //document.getElementById("selAsesor").value = "";
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

//    if (document.getElementById("selAsesor").value == 'José Juan Roldán Ramírez')
//        document.getElementById("hfAsesor").value = "1";
//    if (document.getElementById("selAsesor").value == 'Miguel Ángel Niebla')
//        document.getElementById("hfAsesor").value = "2";
//    if (document.getElementById("selAsesor").value == 'Raúl Juárez')
//        document.getElementById("hfAsesor").value = "3";
//    if (document.getElementById("selAsesor").value == 'Dora Jaquelina Melero Vela')
//        document.getElementById("hfAsesor").value = "4";
//    if (document.getElementById("selAsesor").value == 'Karen Alexis Domínguez Hernández')
//        document.getElementById("hfAsesor").value = "5";
//    if (document.getElementById("selAsesor").value == 'Karol Janeth Herrera Santillán')
//        document.getElementById("hfAsesor").value = "6";
//    if (document.getElementById("selAsesor").value == 'Israel Pimentel De la Torre')
//        document.getElementById("hfAsesor").value = "7";
//    if (document.getElementById("selAsesor").value == 'Laura Isabel Aragón Duarte')
//        document.getElementById("hfAsesor").value = "8";
//    if (document.getElementById("selAsesor").value == 'Clara Denice Nevárez De la Paz')
//        document.getElementById("hfAsesor").value = "9";
//    if (document.getElementById("selAsesor").value == 'Laura Hernández Torres')
//        document.getElementById("hfAsesor").value = "10";
//    if (document.getElementById("selAsesor").value == 'Elvia Beatriz Mendiola Rodríguez')
//        document.getElementById("hfAsesor").value = "11";
//    if (document.getElementById("selAsesor").value == 'Mariana Sardel Vargas Sida')
//        document.getElementById("hfAsesor").value = "12";
//    if (document.getElementById("selAsesor").value == 'Ricardo Ignacio Trejo Burciaga')
//        document.getElementById("hfAsesor").value = "13";
//    if (document.getElementById("selAsesor").value == 'Christian Fernándo Batres Batres')
//        document.getElementById("hfAsesor").value = "14";
//    if (document.getElementById("selAsesor").value == 'Karla Anayance Goytortua Rodríguez')
//        document.getElementById("hfAsesor").value = "15";
//    if (document.getElementById("selAsesor").value == 'Alberto Moriel Labra')
//        document.getElementById("hfAsesor").value = "16";
//    if (document.getElementById("selAsesor").value == 'Juan Francisco Luna Rodríguez')
//        document.getElementById("hfAsesor").value = "17";

//    if (document.getElementById("selTipo").value == 'Casa en Venta')
//        document.getElementById("hfTipo").value = "1";
//    if (document.getElementById("selTipo").value == 'Casa en Renta')
//        document.getElementById("hfTipo").value = "2";
//    if (document.getElementById("selTipo").value == 'Edificio en Venta')
//        document.getElementById("hfTipo").value = "3";
//    if (document.getElementById("selTipo").value == 'Edificio en Renta')
//        document.getElementById("hfTipo").value = "4";
//    if (document.getElementById("selTipo").value == 'Terreno en Venta')
//        document.getElementById("hfTipo").value = "5";
//    if (document.getElementById("selTipo").value == 'Terreno en Renta')
//        document.getElementById("hfTipo").value = "6";
//    if (document.getElementById("selTipo").value == 'Local en Venta')
//        document.getElementById("hfTipo").value = "7";
//    if (document.getElementById("selTipo").value == 'Local en Renta')
//        document.getElementById("hfTipo").value = "8";
//    if (document.getElementById("selTipo").value == 'Bodega en Venta')
//        document.getElementById("hfTipo").value = "9";
//    if (document.getElementById("selTipo").value == 'Bodega en Renta')
//        document.getElementById("hfTipo").value = "10";

//    document.getElementById("hfTerreno").value = document.getElementById("txtTerreno").value;
//    document.getElementById("hfConstruccion").value = document.getElementById("txtConstruccion").value;
//    document.getElementById("hfPrecio").value = document.getElementById("txtPrecio").value;
//    document.getElementById("hfDescripcion").value = document.getElementById("txtDescripcion").value;
//    document.getElementById("hfLink").value = document.getElementById("link").value;

//    document.getElementById("Button1").click();
//}

function GetCode1(a, b, c, d, e, f, g, h, i) {
    //alert(x); // si funciona

    $('#lblTipo').text(a);
    $('#lblidInmueble').text(b + "_" + 1 + ".jpg");
    $('#lblAsesor').text(c);
    $('#lblTelefono').text(d);
    $('#lblTerreno').text(e);
    $('#lblConstruccion').text(f);
    $('#lblPrecio').text(g);
    $('#lblDescripcion').text(h);
    $('#hfNumImgs').val(i);


    $('#imgViewer2').remove();

    $('div#test').append('<div id="imgViewer2" style="height:50px; margin-bottom:2px;"></div>');
    if (i > 0) {
        for (var x = 1 ; x <= i; x++) {
            $('#imgViewer2').append($('<img>', { src: "Cargas/_" + b + "_" + x + ".jpg", onclick: 'panel(this);', style: "margin-left:2px;" }));
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

function panel(x) {

    var medida = $('#imgViewer2').height() + $('#modalHeaderTop').height() + 15;
    //alert(medida);
    $('#background').css('height', $('#modalContent').height() - medida);

    $('#background').css('margin-top', $('#imgViewer2').height() + 14);

    //$('#preview').css('top',(($(window).height()/1) - ($('#preview').height()/1) + $(document).scrollTop()));
    //$('#preview').css('left', ($(document).width()/1) - ($('#preview').width()/1));
    // Cargamos la imagen en la capa grande

    $('#preview').css('width', $('#background').width());
    $('#preview').css('height', $('#background').height());



    var h = $('#preview').height();
    var w = $('#preview').width();

    if (x.height > x.width) {
        //alert(x.height + " Altura");
        $('#content').html("<img src='" + $(x).attr("src") + "'style='width:auto; height: " + ($('#background').height() - 20) + "px;'>");

        $('#preview').css('width', "auto");
        $('#preview').css('height', "auto");
        $('#preview').css('margin-top', (($('#background').height() - $('#preview').height()) / 2));
        $('#preview').css('margin-left', (($('#modalbody1').width() - $('#preview').width()) / 2));

        var h = $('#preview').height();
        var w = $('#preview').width();

        //alert("Alta - Background Altura: " + $('#background').height() + " - Preview Altura: " + $('#preview').height());

        if ($('#background').width() < $('#preview').width()) {
            $('#content').html("<img src='" + $(x).attr("src") + "'style='height:auto; width: " + ($('#background').width() - 20) + "px;'>");
            $('#preview').css('width', "auto");
            $('#preview').css('height', "auto");
            $('#preview').css('margin-top', (($('#background').height() - $('#preview').height()) / 2));
            $('#preview').css('margin-left', (($('#modalbody1').width() - $('#preview').width()) / 2));
        }
    }
    else {
        //alert(x.width + " Anchura"); 
        $('#content').html("<img src='" + $(x).attr("src") + "'style='height:auto; width: " + ($('#background').width() - 20) + "px;'>");
        $('#preview').css('width', "auto");
        $('#preview').css('height', "auto");
        $('#preview').css('margin-top', (($('#background').height() - $('#preview').height()) / 2));
        $('#preview').css('margin-left', (($('#modalbody1').width() - $('#preview').width()) / 2));

        var h = $('#preview').height();
        var w = $('#preview').width();

        //alert("Ancha - Background Altura: " + $('#background').height() + " - Preview Altura: " + $('#preview').height());

        if ($('#background').height() < $('#preview').height()) {
            //alert("2");
            $('#content').html("<img src='" + $(x).attr("src") + "'style='width:auto; height: " + ($('#background').height() - 20) + "px;'>");

            $('#preview').css('width', "auto");
            $('#preview').css('height', "auto");
            $('#preview').css('margin-top', (($('#background').height() - $('#preview').height()) / 2));
            $('#preview').css('margin-left', (($('#modalbody1').width() - $('#preview').width()) / 2));
        }
    }

    // Mostramos las capas
    $('#preview').fadeIn();
    $('#background').fadeIn();
}

function rotate90(numImage, src, callback) {
    //alert("3 - 90");
    //alert("4 - HiddenField" + numImage)
    var img = new Image()
    img.src = src
    img.onload = function () {
        var canvas = document.getElementById('canvas' + numImage)
        canvas.id = "canvas" + numImage
        //alert("5 - " + canvas.id)
        canvas.width = img.height
        canvas.height = img.width
        canvas.style.position = "absolute"
        var body = document.getElementsByTagName("body")[0];
        body.appendChild(canvas);
        var ctx = canvas.getContext("2d")
        ctx.translate(img.height, img.width / img.height)
        ctx.rotate(Math.PI / 2)
        ctx.drawImage(img, 0, 0)


        document.getElementById("HiddenField" + numImage).value = canvas.toDataURL();
        ctx.restore();
    }
}

function rotate180(numImage, src, callback) {
    //alert("180");
    var img = new Image()
    img.src = src
    img.onload = function () {
        var canvas = document.getElementById('canvas' + numImage)
        canvas.id = "canvas" + numImage
        canvas.width = img.width
        canvas.height = img.height
        canvas.style.position = "absolute"
        var ctx = canvas.getContext("2d")
        ctx.translate(img.width / 2, img.height / 2)
        ctx.rotate(Math.PI)
        ctx.drawImage(img, -img.width / 2, -img.height / 2)
        document.getElementById("HiddenField" + numImage).value = canvas.toDataURL();
        //$("canvas" + numImage).remove();
        ctx.restore();
    }
}

function rotate270(numImage, src, callback) {
    //alert("270");
    var img = new Image()
    img.src = src
    img.onload = function () {
        var canvas = document.getElementById('canvas' + numImage)
        canvas.id = "canvas" + numImage
        canvas.width = img.height
        canvas.height = img.width
        canvas.style.position = "absolute"
        var ctx = canvas.getContext("2d")
        ctx.translate(img.height / 2, img.width / 2)
        ctx.rotate(Math.PI * 3 / 2)
        ctx.drawImage(img, -img.width / 2, -img.height / 2)
        document.getElementById("HiddenField" + numImage).value = canvas.toDataURL();
        ctx.restore();
    }
}

function fixExifOrientation($img) {
    var cadena = $img.id,
        separador = "_"; // un espacio en blanco
    arregloDeSubCadenas = cadena.split(separador);
    //alert(arregloDeSubCadenas[0] + " " + arregloDeSubCadenas[1] + " " + arregloDeSubCadenas[2]);
    var numImage = arregloDeSubCadenas[2];
    //alert("1 " + numImage);
    var style = window.getComputedStyle($img);
    var matrix = new WebKitCSSMatrix(style.webkitTransform);
    var degrees;

    degrees = $img.style.transform;
    //alert(degrees);

    if (degrees == "") {
        degrees = 90;
        $($img).css("transform", "rotate(90deg)");
    }
    if (degrees == "rotate(90deg)") {
        degrees = 180;
        $($img).css("transform", "rotate(180deg)");
    }
    if (degrees == "rotate(180deg)") {
        degrees = 270;
        $($img).css("transform", "rotate(270deg)");
    }
    if (degrees == "rotate(270deg)") {
        degrees = 360;
        $($img).css("transform", "rotate(360deg)");
    }
    if (degrees == "rotate(360deg)") {
        degrees = 90;
        $($img).css("transform", "rotate(90deg)");
    }

    var src = document.getElementById("hfs" + numImage).value;

    //alert("2 " + src);

    var img_src = document.createElement('img')
    img_src.src = src
    //document.getElementById('imgViewer').appendChild(img_src) //Pone la misma imagen en el preview

    if (degrees == 90) {
        rotate90(numImage, src, function (res) {
            var img_res = document.createElement('img')
            img_res.src = res
            //document.getElementById('imgViewer').appendChild(img_res)
        })
    }
    if (degrees == 180) {
        rotate180(numImage, src, function (res) {
            var img_res = document.createElement('img')
            img_res.src = res
            //document.getElementById('imgViewer').appendChild(img_res)
        })
    }
    if (degrees == 270) {
        rotate270(numImage, src, function (res) {
            var img_res = document.createElement('img')
            img_res.src = res
            //document.getElementById('imgViewer').appendChild(img_res)
        })
    }

}

function clearAll() {
    marker.setMap(null);
    //alert("limpiando");
    myClear();
}


