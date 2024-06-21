$(document).ready(function (e) {
    //$(window).load(function() {


    var headerheight = $('#headerTop').height();
    var hheight = $(window).height();
    $('#map_canvas').css('height', hheight); // - headerheight);

    $(window).resize(function () {
        var headerheight = $('#headerTop').height();
        var hheight = $(window).height();
        $('#map_canvas').css('height', hheight); // - headerheight);
    });

    $("#amount").keyup(function () {
        if (event.which >= 37 && event.which <= 40) {
            event.preventDefault();
        }
        $(this).val(function (index, value) {
            value = value.replace(/,/g, '');
            return numberWithCommas(value);
        });
        var test = $("#amount").val().toString();
        test = test.replace(/,/g, '');
        var test2 = parseInt(test);
        $("#slider-range").slider('values', 0, test2);
        show();
    });

    $("#amount").keyup(function () {
        if (event.which >= 37 && event.which <= 40) {
            event.preventDefault();
        }
        $(this).val(function (index, value) {
            value = value.replace(/,/g, '');
            return numberWithCommas(value);
        });
        var test = $("#amount").val().toString();
        test = test.replace(/,/g, '');
        var test2 = parseInt(test);
        $("#slider-range").slider('values', 0, test2);
        show();
    });



    $("#txtPhone").keypress(function (e) {
        this.value = this.value.replace(/[^0-9]/g, '');
    });

    //.on("cut copy paste", function (e) {
    //    this.value = this.value.replace(/[^0-9]/g, '');
    //});

    //$(window).resize(function () {
    //    resize();
    //});


});

//function resize() {
//    if ($('#background').height() < $('#background').width()) //BackGround más alto
//    {
//        //alert("Imagen cuadrada, background width más ancho");
//        //alert("y");
//        var bh = parseFloat($('#background').css("height")); //alert(bh);

//        $('#content img').css('height', bh - 10);
//        $('#content img').css('width', 'auto');
//        //$('#content img').css('display', 'block');
//        //$('#content img').css('max-width', 'auto');
//        //$('#content img').css('max-height', 'auto');

//        $('#preview').css('height', 'auto');
//        $('#preview').css('width', 'auto');
//        //$('#preview').css('display', 'block');
//        $('#preview').css('max-width', 'auto');
//        $('#preview').css('max-height', 'auto');

//        $('#content').css('height', 'auto');
//        $('#content').css('width', 'auto');     


//    }
//    else    //BackGround más ancho
//    {
//        //alert("Imagen cuadrada, background height más alto");
//        //alert("x");
//        var bw = parseFloat($('#background').css("width")); //alert(bh);

//        $('#content img').css('height', 'auto');
//        $('#content img').css('width', bw - 10);

//        $('#preview').css('max-height', 'auto');
//        $('#preview').css('max-width', 'auto');

//        $('#content').css('height', 'auto');
//        $('#content').css('width', 'auto');
//    }
//}

//function resize2() {
//    if ($('#background').height() < $('#background').width()) //BackGround más alto
//    {
//        //alert("Imagen cuadrada, background width más ancho");
//        //alert("y");
//        var bh = parseFloat($('#background').css("height")); //alert(bh);

//        $('#content img').css('height', bh - 10);
//        $('#content img').css('width', 'auto');

//        $('#preview').css('max-height', 'auto');
//        $('#preview').css('max-width', 'auto');

//        $('#content').css('height', 'auto');
//        $('#content').css('width', 'auto');



//    }
//    else    //BackGround más ancho
//    {
//        //alert("Imagen cuadrada, background height más alto");
//        //alert("x");
//        var bw = parseFloat($('#background').css("width")); //alert(bh);

//        $('#content img').css('height', 'auto');
//        $('#content img').css('width', bw - 10);

//        $('#preview').css('max-height', 'auto');
//        $('#preview').css('max-width', 'auto');

//        $('#content').css('height', 'auto');
//        $('#content').css('width', 'auto');
//    }
//}

function scheduleA(event) {
    showMan();
}

function scheduleB(event) {
    showMan();
}

function showMan() {

    //alert("This is showMan");

    var e = document.getElementById("cboTipoPropiedad");
    var strUser = e.options[e.selectedIndex].value;

    alert(strUser);

    switch (strUser) {
        case "1":
            Todos();
            break;
        case "2":
            jsMov(2);
            break;
        case "3":
            jsMov(3);
            break;
        case "4":
            jsMov(4);
            break;
        case "5":
            jsMov(5);
            break;
        case "6":
            jsMov(6);
            break;
        case "7":
            jsMov(7);
            break
        case "8":
            jsMov(8);
            break
        case "9":
            jsMov(9);
            break
        case "10":
            jsMov(10);
            break
        case "11":
            jsMov(11);
            break
        case "12":
            jsMov(12);
            break
        case "13":
            jsMov(13);
            break
        case "14":
            jsMov(14);
            break
        case "15":
            jsMov(15);
            break
        case "16":
            jsMov(16);
            break
        case "17":
            jsMov(17);
            break
    }
}

function Todos() {
    //alert("Presionado TODOS en Venta desde proc.");
    var test = $("#ddlViewBy").val().toString();
    test = test.replace(/,/g, '');
    var test2 = parseInt(test);
    alert(test2);

    var test3 = $("#ddlViewBy2").val().toString();
    test3 = test3.replace(/,/g, '');
    var test4 = parseInt(test3);
    alert(test4);

    var e = document.getElementById("cboTipoPropiedad");
    var strUser = e.options[e.selectedIndex].value;

    /*alert(strUser);*/

    alert(markerx.length);

    for (i = 0; i < markerx.length; i++) {

        alert(markerx.length);

        if (markerx[i].customInfo >= test2 && markers[i].customInfo <= test4)
            markerx[i].setVisible(true);
        else
            markerx[i].setVisible(false);
    }

}

function jsMov(x) {

    var test = $("#ddlViewBy").val().toString();
    test = test.replace(/,/g, '');
    var test2 = parseInt(test);
    alert(test2);

    var test3 = $("#ddlViewBy2").val().toString();
    test3 = test3.replace(/,/g, '');
    var test4 = parseInt(test3);
    alert(test4);

    for (i = 0; i < markers.length; i++) {
        if (markers[i].title == x && markers[i].customInfo >= test2 && markers[i].customInfo <= test4)
            markers[i].setVisible(true);
        else
            markers[i].setVisible(false);
    }
}

function ayantoggle() {
    $(".forgot").slideToggle('slow');
}