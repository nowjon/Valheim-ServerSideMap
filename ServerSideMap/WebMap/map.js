// Valheim Server Map - Frontend
// This is a basic implementation. Replace with actual valheim-webmap frontend code.

const API_BASE = window.location.origin;
let mapSize = 2048;
let exploredData = null;
let pins = [];
let players = [];
let mapCanvas = null;
let ctx = null;
let zoom = 1.0;
let panX = 0;
let panY = 0;
let isDragging = false;
let lastMouseX = 0;
let lastMouseY = 0;

// Initialize map
function init() {
    mapCanvas = document.getElementById('map-canvas');
    ctx = mapCanvas.getContext('2d');
    
    // Set canvas size
    mapCanvas.width = window.innerWidth;
    mapCanvas.height = window.innerHeight;
    
    // Load map data
    loadMapInfo();
    loadExploredData();
    loadPins();
    loadPlayers();
    
    // Set up event listeners
    mapCanvas.addEventListener('mousedown', onMouseDown);
    mapCanvas.addEventListener('mousemove', onMouseMove);
    mapCanvas.addEventListener('mouseup', onMouseUp);
    mapCanvas.addEventListener('wheel', onWheel);
    window.addEventListener('resize', onResize);
    
    // Update players periodically
    setInterval(loadPlayers, 2000);
    
    // Start render loop
    requestAnimationFrame(render);
}

function loadMapInfo() {
    fetch(`${API_BASE}/api/mapinfo`)
        .then(r => r.json())
        .then(data => {
            mapSize = data.mapSize;
            updateCanvasSize();
        })
        .catch(err => console.error('Failed to load map info:', err));
}

function loadExploredData() {
    fetch(`${API_BASE}/api/explored`)
        .then(r => r.json())
        .then(data => {
            exploredData = decompressBoolArray(data.data, data.mapSize);
            render();
        })
        .catch(err => console.error('Failed to load explored data:', err));
}

function loadPins() {
    fetch(`${API_BASE}/api/pins`)
        .then(r => r.json())
        .then(data => {
            pins = data;
            render();
        })
        .catch(err => console.error('Failed to load pins:', err));
}

function loadPlayers() {
    fetch(`${API_BASE}/api/players`)
        .then(r => r.json())
        .then(data => {
            players = data;
            updatePlayerList();
            render();
        })
        .catch(err => console.error('Failed to load players:', err));
}

function decompressBoolArray(base64, size) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    
    const result = new Array(size * size);
    let bitIndex = 0;
    let byteIndex = 0;
    
    for (let i = 0; i < result.length; i++) {
        const byte = bytes[byteIndex];
        result[i] = (byte & (1 << bitIndex)) !== 0;
        
        bitIndex++;
        if (bitIndex >= 8) {
            bitIndex = 0;
            byteIndex++;
        }
    }
    
    return result;
}

function worldToScreen(wx, wy) {
    const scale = zoom;
    const sx = (wx / mapSize) * mapCanvas.width * scale + panX;
    const sy = (wy / mapSize) * mapCanvas.height * scale + panY;
    return [sx, sy];
}

function screenToWorld(sx, sy) {
    const scale = zoom;
    const wx = ((sx - panX) / (mapCanvas.width * scale)) * mapSize;
    const wy = ((sy - panY) / (mapCanvas.height * scale)) * mapSize;
    return [wx, wy];
}

function render() {
    if (!ctx || !exploredData) return;
    
    // Clear canvas
    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, mapCanvas.width, mapCanvas.height);
    
    // Draw explored areas
    if (exploredData) {
        const pixelSize = Math.max(1, Math.ceil(zoom));
        const startX = Math.max(0, Math.floor((-panX / zoom) / (mapCanvas.width / mapSize)));
        const endX = Math.min(mapSize, Math.ceil((mapCanvas.width - panX) / zoom / (mapCanvas.width / mapSize)));
        const startY = Math.max(0, Math.floor((-panY / zoom) / (mapCanvas.height / mapSize)));
        const endY = Math.min(mapSize, Math.ceil((mapCanvas.height - panY) / zoom / (mapCanvas.height / mapSize)));
        
        ctx.fillStyle = '#2a4a2a';
        for (let y = startY; y < endY; y += pixelSize) {
            for (let x = startX; x < endX; x += pixelSize) {
                const idx = y * mapSize + x;
                if (exploredData[idx]) {
                    const [sx, sy] = worldToScreen(x, y);
                    ctx.fillRect(sx, sy, pixelSize * zoom, pixelSize * zoom);
                }
            }
        }
    }
    
    // Draw pins
    const pinsOverlay = document.getElementById('pins-overlay');
    pinsOverlay.innerHTML = '';
    pins.forEach(pin => {
        const [sx, sy] = worldToScreen(pin.pos.x, pin.pos.z);
        if (sx >= -50 && sx <= mapCanvas.width + 50 && sy >= -50 && sy <= mapCanvas.height + 50) {
            const marker = document.createElement('div');
            marker.className = 'pin-marker';
            marker.style.left = sx + 'px';
            marker.style.top = sy + 'px';
            marker.style.backgroundColor = getPinColor(pin.type);
            marker.title = pin.name;
            pinsOverlay.appendChild(marker);
        }
    });
    
    // Draw players
    const playersOverlay = document.getElementById('players-overlay');
    playersOverlay.innerHTML = '';
    players.forEach(player => {
        if (!player.visible) return;
        const [sx, sy] = worldToScreen(player.pos.x, player.pos.z);
        if (sx >= -50 && sx <= mapCanvas.width + 50 && sy >= -50 && sy <= mapCanvas.height + 50) {
            const marker = document.createElement('div');
            marker.className = 'player-marker';
            marker.style.left = sx + 'px';
            marker.style.top = sy + 'px';
            marker.title = player.name;
            playersOverlay.appendChild(marker);
        }
    });
}

function getPinColor(type) {
    const colors = {
        0: '#ff0000', // Icon0 (dot)
        1: '#ff8800', // Icon1 (fire)
        2: '#888888', // Icon2 (mine)
        3: '#00ff00', // Icon3 (house)
        4: '#0000ff', // Icon4 (cave)
    };
    return colors[type] || '#ffffff';
}

function updatePlayerList() {
    const list = document.getElementById('player-list');
    list.innerHTML = '<strong>Players:</strong><br>';
    if (players.length === 0) {
        list.innerHTML += 'No players online';
    } else {
        players.forEach(player => {
            if (player.visible) {
                const item = document.createElement('div');
                item.className = 'player-item';
                item.textContent = player.name;
                list.appendChild(item);
            }
        });
    }
}

function updateCanvasSize() {
    mapCanvas.width = window.innerWidth;
    mapCanvas.height = window.innerHeight;
    render();
}

function onMouseDown(e) {
    isDragging = true;
    lastMouseX = e.clientX;
    lastMouseY = e.clientY;
}

function onMouseMove(e) {
    if (isDragging) {
        panX += e.clientX - lastMouseX;
        panY += e.clientY - lastMouseY;
        lastMouseX = e.clientX;
        lastMouseY = e.clientY;
        render();
    }
}

function onMouseUp(e) {
    isDragging = false;
}

function onWheel(e) {
    e.preventDefault();
    const delta = e.deltaY > 0 ? 0.9 : 1.1;
    zoom = Math.max(0.1, Math.min(5.0, zoom * delta));
    document.getElementById('zoom-level').textContent = zoom.toFixed(2);
    render();
}

function onResize() {
    updateCanvasSize();
}

// Initialize when page loads
window.addEventListener('load', init);

