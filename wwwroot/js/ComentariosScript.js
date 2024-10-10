document.addEventListener("DOMContentLoaded", function () {
    cargarComentarios();

    const comentarioForm = document.getElementById("comentarioForm");
    if (comentarioForm) {
        comentarioForm.addEventListener("submit", async function (event) {
            event.preventDefault();

            const comentarioTexto = document.getElementById("comentario").value;
            const nivel = document.getElementById("nivel").value;

            if (!comentarioTexto || !nivel) {
                console.error("Los campos Comentario y Nivel son obligatorios");
                return;
            }

            const antiforgeryTokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
            if (!antiforgeryTokenElement) {
                console.error("El token antifalsificación no se encuentra en el DOM.");
                return;
            }
            const antiforgeryToken = antiforgeryTokenElement.value;

            const comentario = {
                comentarioTexto: comentarioTexto,
                nivel: nivel
            };
            console.log("Datos enviados:", comentario);

            try {
                const response = await fetch("/api/Comentarios", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "X-CSRF-Token": antiforgeryToken // Cambiar de 'RequestVerificationToken' a 'X-CSRF-Token'
                    },
                    body: JSON.stringify(comentario)
                });

                if (!response.ok) {
                    throw new Error("Error al registrar el comentario");
                }

                document.getElementById("mensaje-resultado").innerText = "Comentario registrado con éxito";
                document.getElementById("mensaje-resultado").style.display = "block";
                cargarComentarios(); // Recargar los comentarios
                comentarioForm.reset();
            } catch (error) {
                console.error(error);
                document.getElementById("mensaje-resultado").innerText = "Error al registrar el comentario";
                document.getElementById("mensaje-resultado").style.display = "block";
            }
        });
    } else {
        console.error("El formulario de comentario no se encuentra en el DOM.");
    }
});

async function cargarComentarios() {
    try {
        const response = await fetch("/api/Comentarios/");
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
        console.error(error);
    }
}