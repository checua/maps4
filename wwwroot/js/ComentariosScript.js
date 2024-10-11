document.addEventListener("DOMContentLoaded", function () {
    cargarComentarios();

    const comentarioForm = document.getElementById("comentarioForm");
    if (comentarioForm) {

        const formData = new FormData();
        comentarioForm.addEventListener("submit", async function (event) {
            event.preventDefault();

            const comentarioTexto = document.getElementById("comentario").value;
            const nivel = document.getElementById("nivel").value;

            if (!comentarioTexto || !nivel) {
                console.error("Los campos Comentario y Nivel son obligatorios");
                return;
            }

            formData.set('comentarioTexto', comentarioTexto);
            formData.set('nivel', nivel);
            formData.set('correo', ""); // Se obtiene en el backend
            formData.set('nombre', ""); // Se obtiene en el backend
            formData.set('telefono', ""); // Se obtiene en el backend
            formData.set('fechaComentario', "");
            formData.set('fechaExpiracion', "");
            formData.set('activo', true);

            const url = "Comentarios/RegistrarComentario";
            try {
                const response = await fetch(url, {
                    method: 'POST',
                    body: formData
                });

                const responseData = await response.json();

                if (response.ok && responseData.success) {
                    document.getElementById("mensaje-resultado").innerText = "Comentario registrado con éxito";
                    document.getElementById("mensaje-resultado").style.display = "block";

                    // Recargar comentarios de manera asíncrona sin recargar toda la página
                    await cargarComentarios();

                    comentarioForm.reset();
                } else {
                    document.getElementById("mensaje-resultado").innerText = "Error al registrar el comentario";
                    document.getElementById("mensaje-resultado").style.display = "block";
                }
            } catch (error) {
                console.error('Error al enviar datos:', error);
                alert("Error en la red o servidor");
            }
        });
    } else {
        console.error("El formulario de comentario no se encuentra en el DOM.");
    }
});

async function cargarComentarios() {
    try {
        const response = await fetch("Comentarios/GetComentariosActivos");
        if (!response.ok) {
            throw new Error("Error al cargar los comentarios");
        }

        const comentarios = await response.json();
        const container = document.getElementById("comentarios-container");
        container.innerHTML = "";

        comentarios.forEach(comentario => {
            const comentarioDiv = document.createElement("div");
            comentarioDiv.className = "comentario";
            comentarioDiv.innerHTML = `<p><strong>${comentario.nivel}:</strong> ${comentario.comentarioTexto}</p>`;
            container.appendChild(comentarioDiv);
        });
    } catch (error) {
        console.error("Error al cargar los comentarios:", error);
    }
}
