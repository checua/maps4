window.onload = function () {
    var latLng = new google.maps.LatLng(24.02, -104.62);
    var opciones = {
        center: latLng,
        zoom: 12,
        mapTypeId: google.maps.MapTypeId.roadmap,
        disableDefaultUI: true
    };
    var map = new google.maps.Map(document.getElementById('map_canvas'), opciones);


    //alert("hola");
}