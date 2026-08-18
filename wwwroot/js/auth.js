$(function () {
    // Reemplaza el handler legado de index.js que enviaba credenciales por GET.
    $(document).off("click", ".boton-iniciar-sesion");

    $(document).on("click", ".boton-iniciar-sesion", async function (event) {
        event.preventDefault();

        const correo = $("#correo").val().trim();
        const contra = $("#contra").val();

        if (!correo || !contra) {
            Swal.fire("Faltan datos", "Captura correo y contraseña.", "warning");
            return;
        }

        try {
            const body = new URLSearchParams();
            body.append("correo", correo);
            body.append("contra", contra);

            const response = await fetch("/Inicio/IniciarSesion", {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8"
                },
                body: body.toString()
            });

            if (!response.ok) {
                let mensaje = "Usuario o contraseña equivocados.";

                try {
                    const error = await response.json();
                    if (error && error.mensaje) {
                        mensaje = error.mensaje;
                    }
                } catch (_) {
                    // Mantener mensaje genérico si la respuesta no es JSON.
                }

                throw new Error(mensaje);
            }

            const data = await response.json();

            $("#modalEmpleado").modal("hide");
            $("#lnkAcceso").text(data.correo || correo);
            $("#correo").val("");
            $("#contra").val("");
        } catch (error) {
            Swal.fire("No se pudo iniciar sesión", error.message, "error");
        }
    });
});
