let isScrollingManually = false;
let isPaused = false; // Para pausar el desplazamiento
let isMarquesinaVisible = false; // Estado de visibilidad de la marquesina
const cortinaAnuncios = document.getElementById('cortina-anuncios');
const anunciosScrollContainer = document.getElementById('anuncios-scroll-container');
let scrollSpeed = 1; // Velocidad estándar lenta


// Función para cargar comentarios desde el servidor
async function cargarComentarios(tipoPropiedadId = 0) {
    try {
        const response = await fetch("Comentarios/GetComentariosActivos");
        if (!response.ok) {
            throw new Error("Error al cargar los comentarios");
        }

        const comentarios = await response.json();
        const container = document.getElementById("cortina-anuncios");
        container.innerHTML = ""; // Limpiar contenedor antes de cargar los comentarios

        comentarios
            .filter(comentario => tipoPropiedadId == 0 || comentario.tipoInmuebleSolicitado == tipoPropiedadId)
            .forEach(comentario => {
                const comentarioDiv = document.createElement("div");
                comentarioDiv.className = `comentario ${comentario.nivel.toLowerCase()}`;

                comentarioDiv.innerHTML = `
                    <p><strong>${comentario.nivel}:</strong> ${comentario.comentarioTexto}</p>
                    <p><strong>Nombre:</strong> ${comentario.nombre}<br>${comentario.telefono}</p>
                `;
                container.appendChild(comentarioDiv);
            });
    } catch (error) {
        console.error("Error al cargar los comentarios:", error);
    }
}

// Inicia el desplazamiento automático
function startAutoScroll() {
    const containerHeight = anunciosScrollContainer.scrollHeight;
    anunciosScrollContainer.scrollTop = 0; // Empezar desde arriba

    const scroll = () => {
        if (!isScrollingManually && !isPaused) {
            // Desplazar hacia abajo
            anunciosScrollContainer.scrollTop += scrollSpeed;

            // Si llegamos al final, regresar al principio
            if (anunciosScrollContainer.scrollTop >= containerHeight - anunciosScrollContainer.clientHeight) {
                anunciosScrollContainer.scrollTop = 0;
            }
        }
    };

    // Ejecutar el desplazamiento cada 50ms para un movimiento suave
    setInterval(scroll, 60);
}

// Pausa y reanuda la animación al hacer clic en cualquier comentario
function toggleScroll() {
    isPaused = !isPaused;
}

// Escuchar clics en los comentarios para pausar/reanudar
document.getElementById('cortina-anuncios').addEventListener('click', toggleScroll);

// Detectar desplazamiento manual
anunciosScrollContainer.addEventListener('scroll', () => {
    isScrollingManually = true;
    isPaused = true; // Pausar el desplazamiento automático mientras se usa el scroll manual

    // Detener el desplazamiento automático mientras el usuario está interactuando
    clearTimeout(anunciosScrollContainer.autoScrollTimeout);
    anunciosScrollContainer.autoScrollTimeout = setTimeout(() => {
        isScrollingManually = false;
        isPaused = false; // Reanudar el desplazamiento después de 2 segundos sin interacción
    });
});

// Función para mostrar/ocultar la marquesina
function toggleMarquesina() {
    const cortinaAnunciosContainer = document.getElementById('cortina-anuncios-container');
    const pestanaAnuncios = document.getElementById('pestana-anuncios');
    const menuFlotante = document.getElementById('menu-flotante');

    if (isMarquesinaVisible) {
        cortinaAnunciosContainer.style.right = '-300px'; // Ocultar marquesina
        pestanaAnuncios.style.right = '0'; // Regresar pestaña al borde derecho de la pantalla
        pestanaAnuncios.classList.remove('expanded');
        pestanaAnuncios.classList.add('collapsed');
    } else {
        cortinaAnunciosContainer.style.right = '0'; // Mostrar marquesina
        pestanaAnuncios.style.right = '300px'; // Mover la pestaña al borde izquierdo de la marquesina
        pestanaAnuncios.classList.remove('collapsed');
        pestanaAnuncios.classList.add('expanded');
    }

    // Desaparecer menú flotante al mostrar la marquesina
    if (menuFlotante) {
        menuFlotante.style.display = 'none';
    }

    isMarquesinaVisible = !isMarquesinaVisible;
}

// Inicializar al cargar la página
document.addEventListener('DOMContentLoaded', () => {
    cargarComentarios();
});
