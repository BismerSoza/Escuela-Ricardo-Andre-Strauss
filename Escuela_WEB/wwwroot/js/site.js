// site.js — comportamiento global de MiniCampus

document.addEventListener('DOMContentLoaded', () => {
    // Menú lateral en pantallas pequeñas
    const boton = document.getElementById('btnToggleSidebar');
    const fondo = document.getElementById('sidebarBackdrop');
    boton?.addEventListener('click', () => document.body.classList.toggle('sidebar-abierto'));
    fondo?.addEventListener('click', () => document.body.classList.remove('sidebar-abierto'));

    // Campanita de notificaciones
    if (document.getElementById('campanaBadge')) {
        cargarNotificaciones();
        setInterval(cargarNotificaciones, 60000);

        document.getElementById('btnMarcarLeidas')?.addEventListener('click', async () => {
            try {
                await fetch('/Home/MarcarLeidas', { method: 'POST' });
                await cargarNotificaciones();
            } catch { /* sin conexión: se ignora */ }
        });
    }
});

function escaparHtml(texto) {
    const div = document.createElement('div');
    div.textContent = texto ?? '';
    return div.innerHTML;
}

// Consulta cuántos avisos nuevos y mensajes sin leer tiene el usuario
// y actualiza el contador rojo de la campanita y la lista desplegable.
async function cargarNotificaciones() {
    const badge = document.getElementById('campanaBadge');
    const lista = document.getElementById('notifLista');
    if (!badge || !lista) return;

    try {
        const respuesta = await fetch('/Home/Notificaciones', { headers: { 'Accept': 'application/json', 'X-Requested-With': 'fetch' } });
        if (respuesta.status === 401) { window.location.href = '/?expirada=1'; return; }
        if (!respuesta.ok) return;
        const datos = await respuesta.json();

        badge.textContent = datos.total > 9 ? '9+' : datos.total;
        badge.classList.toggle('d-none', datos.total === 0);

        const navMsg = document.getElementById('navMsgBadge');
        if (navMsg) {
            navMsg.textContent = datos.mensajes;
            navMsg.classList.toggle('d-none', datos.mensajes === 0);
        }

        let html = '';
        if (datos.mensajes > 0) {
            html += `<a class="notif-item" href="/Mensaje/Chat">
                <div class="notif-icono"><i class="bi bi-chat-dots"></i></div>
                <div><div class="notif-titulo">Tiene ${datos.mensajes} mensaje(s) sin leer</div>
                <div class="notif-sub">Ir a mensajería</div></div></a>`;
        }
        (datos.avisos || []).forEach(a => {
            const importante = a.prioridad === 'Importante';
            html += `<a class="notif-item" href="/Aviso">
                <div class="notif-icono ${importante ? 'importante' : ''}"><i class="bi bi-megaphone"></i></div>
                <div><div class="notif-titulo">${escaparHtml(a.titulo)}</div>
                <div class="notif-sub">${escaparHtml(a.autor)} · ${escaparHtml(a.fecha)}</div></div></a>`;
        });

        lista.innerHTML = html || `<div class="notif-vacio"><i class="bi bi-bell-slash fs-3 d-block mb-1"></i>No tiene notificaciones nuevas</div>`;
    } catch {
        // Si la sesión venció o no hay red, simplemente no se muestra nada.
    }
}

// Muestra una alerta Bootstrap en la parte superior del contenido
function mostrarAlertaGlobal(mensaje, tipo = 'danger') {
    const contenedor = document.getElementById('alertaGlobal');
    if (!contenedor) { alert(mensaje); return; }
    const id = 'alerta_' + Date.now();
    contenedor.insertAdjacentHTML('beforeend', `
        <div id="${id}" class="alert alert-${tipo} alert-dismissible fade show" role="alert">
            <i class="bi bi-exclamation-triangle me-1"></i>${escaparHtml(mensaje)}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>`);
    setTimeout(() => document.getElementById(id)?.remove(), 8000);
}

// fetch con manejo de errores: si falla, avisa al usuario en vez de dejar la pantalla muerta
async function fetchSeguro(url, opciones = {}) {
    try {
        opciones.headers = Object.assign({ 'X-Requested-With': 'fetch' }, opciones.headers || {});
        const respuesta = await fetch(url, opciones);

        // Sesión vencida o cuenta desactivada por la dirección: volver al inicio de sesión.
        if (respuesta.status === 401) {
            window.location.href = '/?expirada=1';
            return respuesta;
        }

        if (!respuesta.ok) {
            let mensaje = 'Ocurrió un error al procesar la solicitud.';
            try {
                const datos = await respuesta.clone().json();
                mensaje = datos.mensaje || datos.title || mensaje;
            } catch {
                const texto = await respuesta.text().catch(() => '');
                if (texto) mensaje = texto;
            }
            mostrarAlertaGlobal(mensaje);
        }
        return respuesta;
    } catch (error) {
        mostrarAlertaGlobal('No fue posible conectar con el servidor. Verifique su conexión e intente nuevamente.');
        throw error;
    }
}

// Gráfico de barras estándar para los tableros (requiere Chart.js cargado en la vista)
function crearGraficoBarras(idCanvas, etiquetas, valores, color) {
    const canvas = document.getElementById(idCanvas);
    if (!canvas || typeof Chart === 'undefined') return;
    new Chart(canvas, {
        type: 'bar',
        data: {
            labels: etiquetas,
            datasets: [{ data: valores, backgroundColor: color || '#1b365d', borderRadius: 4, maxBarThickness: 56 }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: { callbacks: { label: (c) => ' Promedio: ' + c.parsed.y } }
            },
            scales: {
                y: { beginAtZero: true, max: 100, ticks: { stepSize: 20 }, grid: { color: '#eef1f5' } },
                x: { grid: { display: false } }
            }
        }
    });
}

window.addEventListener('error', (e) => console.error('Error no controlado:', e.error || e.message));
window.addEventListener('unhandledrejection', (e) => console.error('Promesa rechazada:', e.reason));
