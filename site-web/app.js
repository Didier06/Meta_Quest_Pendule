document.addEventListener('DOMContentLoaded', () => {
    // --- Tabs Logic ---
    const tabBtns = document.querySelectorAll('.tab-btn');
    const tabContents = document.querySelectorAll('.tab-content');

    tabBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            // Remove active classes
            tabBtns.forEach(b => b.classList.remove('active'));
            tabContents.forEach(c => c.classList.remove('active'));

            // Add active class to clicked tab
            btn.classList.add('active');
            const targetId = btn.getAttribute('data-tab');
            document.getElementById(targetId).classList.add('active');
        });
    });

    // --- Chart.js Configuration ---
    Chart.defaults.color = '#475569';
    Chart.defaults.font.family = "'Inter', sans-serif";

    // Custom plugin for vertical and horizontal crosshairs
    const crosshairPlugin = {
        id: 'crosshair',
        afterDraw: chart => {
            if (chart.tooltip?._active?.length) {
                const x = chart.tooltip._active[0].element.x;
                const yAxis = chart.scales.y;
                const xAxis = chart.scales.x;
                const ctx = chart.ctx;
                
                ctx.save();
                ctx.lineWidth = 1;
                ctx.strokeStyle = 'rgba(0, 0, 0, 0.3)';
                ctx.setLineDash([5, 5]);

                // Ligne verticale (temps)
                ctx.beginPath();
                ctx.moveTo(x, yAxis.top);
                ctx.lineTo(x, yAxis.bottom);
                ctx.stroke();

                // Lignes horizontales (angles) pour chaque point actif
                chart.tooltip._active.forEach(activePoint => {
                    const y = activePoint.element.y;
                    ctx.beginPath();
                    ctx.moveTo(xAxis.left, y);
                    ctx.lineTo(xAxis.right, y);
                    ctx.stroke();
                });

                ctx.restore();
            }
        }
    };

    const commonChartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        animation: {
            duration: 0 // Disable animation for better performance with real-time data
        },
        elements: {
            point: {
                radius: 0,
                hitRadius: 10,
                hoverRadius: 4
            },
            line: {
                tension: 0.4, // Smooth curves
                borderWidth: 2
            }
        },
        scales: {
            x: {
                type: 'linear',
                title: { display: true, text: 'Temps (s)', color: '#475569' },
                grid: { color: 'rgba(0, 0, 0, 0.05)' },
                ticks: {
                    maxTicksLimit: 10,
                    callback: function(value) { return value.toFixed(1); }
                }
            },
            y: {
                title: { display: true, text: 'Angle (degrés)', color: '#475569' },
                grid: { color: 'rgba(0, 0, 0, 0.05)' },
                suggestedMin: -90,
                suggestedMax: 90
            }
        },
        plugins: {
            legend: {
                labels: { color: '#0f172a', usePointStyle: true, boxWidth: 8 }
            },
            tooltip: {
                mode: 'index',
                intersect: false,
                backgroundColor: 'rgba(255, 255, 255, 0.95)',
                titleColor: '#0f172a',
                bodyColor: '#1e293b',
                borderColor: 'rgba(0,0,0,0.1)',
                borderWidth: 1
            }
        },
        interaction: {
            mode: 'nearest',
            axis: 'x',
            intersect: false
        }
    };

    // Initialize Simple Pendulum Chart
    const ctxSimple = document.getElementById('chart-simple').getContext('2d');
    const chartSimple = new Chart(ctxSimple, {
        type: 'line',
        data: {
            datasets: [{
                label: 'Angle θ',
                data: [],
                borderColor: '#3b82f6',
                backgroundColor: 'rgba(59, 130, 246, 0.1)',
                fill: true
            }]
        },
        options: commonChartOptions,
        plugins: [crosshairPlugin]
    });

    // Initialize Coupled Pendulum Chart
    const ctxCouple = document.getElementById('chart-couple').getContext('2d');
    const chartCouple = new Chart(ctxCouple, {
        type: 'line',
        data: {
            datasets: [
                {
                    label: 'Angle θ₁',
                    data: [],
                    borderColor: '#3b82f6',
                    backgroundColor: 'transparent'
                },
                {
                    label: 'Angle θ₂',
                    data: [],
                    borderColor: '#10b981',
                    backgroundColor: 'transparent'
                }
            ]
        },
        options: commonChartOptions,
        plugins: [crosshairPlugin]
    });

    // --- 2D Pendulum Animation ---
    const canvasSimple = document.getElementById('anim-simple');
    const ctxAnim = canvasSimple.getContext('2d');

    function drawPendulum(ctx, angleDeg, canvasWidth, canvasHeight) {
        ctx.clearRect(0, 0, canvasWidth, canvasHeight);
        
        // Pivot point
        const originX = canvasWidth / 2;
        const originY = 50;
        const length = 200; // Visual length
        
        // Calculate bob position
        const angleRad = -angleDeg * Math.PI / 180; // Negative to match visual rotation usually
        const bobX = originX + length * Math.sin(angleRad);
        const bobY = originY + length * Math.cos(angleRad);
        
        // Draw axis/support
        ctx.beginPath();
        ctx.moveTo(originX - 20, originY);
        ctx.lineTo(originX + 20, originY);
        ctx.lineWidth = 4;
        ctx.strokeStyle = '#475569';
        ctx.stroke();

        // Draw rod shadow
        ctx.beginPath();
        ctx.moveTo(originX, originY);
        ctx.lineTo(bobX, bobY);
        ctx.lineWidth = 12;
        ctx.strokeStyle = 'rgba(249, 115, 22, 0.4)'; // Orange outline like Unity
        ctx.stroke();

        // Draw rod core
        ctx.beginPath();
        ctx.moveTo(originX, originY);
        ctx.lineTo(bobX, bobY);
        ctx.lineWidth = 6;
        ctx.strokeStyle = '#3b82f6'; // Blue core like Unity arrow
        ctx.stroke();

        // Draw bob (Mass) with marble texture simulation
        ctx.beginPath();
        ctx.arc(bobX, bobY, 35, 0, 2 * Math.PI);
        const gradient = ctx.createRadialGradient(bobX - 10, bobY - 10, 5, bobX, bobY, 35);
        gradient.addColorStop(0, '#fecdd3'); // light pink
        gradient.addColorStop(0.5, '#e11d48'); // rose red
        gradient.addColorStop(1, '#881337'); // dark rose
        
        ctx.fillStyle = gradient;
        ctx.fill();
        ctx.lineWidth = 4;
        ctx.strokeStyle = 'rgba(249, 115, 22, 0.8)'; // Orange outline for the bob
        ctx.stroke();

        // Draw pivot pin
        ctx.beginPath();
        ctx.arc(originX, originY, 6, 0, 2 * Math.PI);
        ctx.fillStyle = '#f8fafc';
        ctx.fill();
        ctx.lineWidth = 2;
        ctx.strokeStyle = '#475569';
        ctx.stroke();
    }

    // Initial draw
    drawPendulum(ctxAnim, 0, canvasSimple.width, canvasSimple.height);

    // --- MQTT Logic ---
    let mqttClient = null;
    const globalStatusIndicator = document.getElementById('global-status-indicator');
    const globalStatusText = document.getElementById('global-status-text');
    const connectBtn = document.getElementById('mqtt-connect-btn');
    const disconnectBtn = document.getElementById('mqtt-disconnect-btn');
    const logContainer = document.getElementById('mqtt-log-container');

    // Auto-generate client ID
    const generatedClientId = 'lab_client_' + Math.random().toString(16).substr(2, 8);

    function addLog(message, type = 'message') {
        const entry = document.createElement('div');
        entry.className = `log-entry ${type}`;
        
        const now = new Date();
        const timeStr = `${now.getHours().toString().padStart(2, '0')}:${now.getMinutes().toString().padStart(2, '0')}:${now.getSeconds().toString().padStart(2, '0')}.${now.getMilliseconds().toString().padStart(3, '0')}`;
        
        entry.innerHTML = `<span class="time">[${timeStr}]</span>${message}`;
        logContainer.appendChild(entry);
        logContainer.scrollTop = logContainer.scrollHeight;
        
        // Keep only last 100 logs
        if (logContainer.children.length > 100) {
            logContainer.removeChild(logContainer.firstChild);
        }
    }

    function updateConnectionStatus(status, text) {
        globalStatusIndicator.className = 'status-indicator';
        globalStatusIndicator.classList.add(status);
        globalStatusText.textContent = text;
        
        const badge = globalStatusIndicator.parentElement;
        if (status === 'connected') {
            badge.style.borderColor = 'var(--success-color)';
            badge.style.color = 'var(--text-primary)';
            connectBtn.style.display = 'none';
            disconnectBtn.style.display = 'flex';
        } else if (status === 'connecting') {
            badge.style.borderColor = 'var(--warning-color)';
            badge.style.color = 'var(--warning-color)';
        } else {
            badge.style.borderColor = 'var(--border-color)';
            badge.style.color = 'var(--text-secondary)';
            connectBtn.style.display = 'flex';
            disconnectBtn.style.display = 'none';
        }
    }

    connectBtn.addEventListener('click', () => {
        const host = document.getElementById('mqtt-host').value || 'localhost';
        const port = (host.includes('localhost') || host.match(/^[0-9.]+$/)) ? 9001 : 443;
        const clientId = generatedClientId;
        const username = document.getElementById('mqtt-user').value;
        const password = document.getElementById('mqtt-pass').value;

        // Determine protocol
        const protocol = (host.includes('localhost') || host.match(/^[0-9.]+$/)) ? 'ws' : 'wss';
        const url = `${protocol}://${host}:${port}/mqtt`;

        updateConnectionStatus('connecting', 'Connexion en cours...');
        addLog(`Tentative de connexion à ${url}...`, 'system');

        const options = {
            clientId: clientId,
            keepalive: 60,
            clean: true,
            reconnectPeriod: 5000,
            connectTimeout: 30 * 1000,
        };

        if (username) options.username = username;
        if (password) options.password = password;

        try {
            mqttClient = mqtt.connect(url, options);

            mqttClient.on('connect', () => {
                updateConnectionStatus('connected', 'Connecté');
                addLog('Connecté au broker MQTT avec succès.', 'success');
                
                // Subscribe to topics
                const topics = [
                    'FABLAB_21_22/Unity/meta/pendule/out/',
                    'FABLAB_21_22/Unity/meta/pend_coupl/out/'
                ];
                mqttClient.subscribe(topics, (err) => {
                    if (!err) {
                        addLog(`Abonné aux topics : ${topics.join(', ')}`, 'system');
                    } else {
                        addLog(`Erreur d'abonnement : ${err.message}`, 'error');
                    }
                });
            });

            mqttClient.on('reconnect', () => {
                updateConnectionStatus('connecting', 'Reconnexion...');
                addLog('Tentative de reconnexion...', 'system');
            });

            mqttClient.on('error', (err) => {
                addLog(`Erreur MQTT : ${err.message}`, 'error');
            });

            mqttClient.on('close', () => {
                if (mqttClient && mqttClient.connected) {
                    updateConnectionStatus('disconnected', 'Déconnecté');
                    addLog('Connexion fermée.', 'system');
                }
            });

            mqttClient.on('message', (topic, message) => {
                try {
                    const payload = message.toString();
                    const data = JSON.parse(payload);
                    handleIncomingData(topic, data);
                } catch (e) {
                    addLog(`Erreur parsing message sur <span class="topic">${topic}</span>`, 'error');
                }
            });

        } catch (e) {
            updateConnectionStatus('disconnected', 'Erreur');
            addLog(`Erreur d'initialisation MQTT : ${e.message}`, 'error');
        }
    });

    disconnectBtn.addEventListener('click', () => {
        if (mqttClient) {
            mqttClient.end();
            mqttClient = null;
            updateConnectionStatus('disconnected', 'Déconnecté');
            addLog('Déconnexion manuelle effectuée.', 'system');
        }
    });

    // --- Data Processing ---
    const MAX_DATA_POINTS = 300; // Limit points to avoid performance issues
    let startTimeSimple = null;
    let startTimeCouple = null;

    function handleIncomingData(topic, data) {
        let relativeTime;

        if (data.id === 'pendule_simple_1') {
            if (startTimeSimple === null) startTimeSimple = Date.now();
            relativeTime = data.temps !== undefined ? data.temps : (Date.now() - startTimeSimple) / 1000;
            
            if (data.angle !== undefined) {
                const dataset = chartSimple.data.datasets[0];
                dataset.data.push({ x: relativeTime, y: data.angle });
                
                if (dataset.data.length > MAX_DATA_POINTS) {
                    dataset.data.shift();
                }
                chartSimple.update('none');
                
                // Animate 2D Pendulum
                drawPendulum(ctxAnim, data.angle, canvasSimple.width, canvasSimple.height);
            }
        } 
        else if (data.id === 'pendules_couples') {
            if (startTimeCouple === null) startTimeCouple = Date.now();
            relativeTime = data.temps !== undefined ? data.temps : (Date.now() - startTimeCouple) / 1000;
            
            let updated = false;
            if (data.ang1 !== undefined) {
                const dataset = chartCouple.data.datasets[0];
                dataset.data.push({ x: relativeTime, y: data.ang1 });
                if (dataset.data.length > MAX_DATA_POINTS) dataset.data.shift();
                updated = true;
            }
            if (data.ang2 !== undefined) {
                const dataset = chartCouple.data.datasets[1];
                dataset.data.push({ x: relativeTime, y: data.ang2 });
                if (dataset.data.length > MAX_DATA_POINTS) dataset.data.shift();
                updated = true;
            }
            if (updated) chartCouple.update('none');
        }
    }

    // --- Forms Submissions ---
    document.getElementById('form-simple').addEventListener('submit', (e) => {
        e.preventDefault();
        
        if (!mqttClient || !mqttClient.connected) {
            alert('Veuillez vous connecter au serveur MQTT avant d\'envoyer des commandes.');
            return;
        }

        const payload = {
            id: "pendule_simple_1",
            ang_init: parseFloat(document.getElementById('simple-angle').value),
            alpha: parseFloat(document.getElementById('simple-frottement').value),
            m: parseFloat(document.getElementById('simple-masse').value),
            longueur: parseFloat(document.getElementById('simple-longueur').value)
        };

        const topic = 'FABLAB_21_22/Unity/meta/pendule/in/';
        mqttClient.publish(topic, JSON.stringify(payload));
        addLog(`Commande envoyée sur <span class="topic">${topic}</span>`, 'system');
        
        // Reset chart
        chartSimple.data.datasets[0].data = [];
        chartSimple.update();
        startTimeSimple = null; // Reset relative time
    });

    document.getElementById('form-couple').addEventListener('submit', (e) => {
        e.preventDefault();
        
        if (!mqttClient || !mqttClient.connected) {
            alert('Veuillez vous connecter au serveur MQTT avant d\'envoyer des commandes.');
            return;
        }

        const payload = {
            id: "pendules_couples",
            ang_init1: parseFloat(document.getElementById('couple-angle1').value),
            ang_init2: parseFloat(document.getElementById('couple-angle2').value),
            alpha1: parseFloat(document.getElementById('couple-alpha1').value),
            alpha2: parseFloat(document.getElementById('couple-alpha2').value),
            Kc: parseFloat(document.getElementById('couple-k').value)
        };

        const topic = 'FABLAB_21_22/Unity/meta/pend_coupl/in/';
        mqttClient.publish(topic, JSON.stringify(payload));
        addLog(`Commande envoyée sur <span class="topic">${topic}</span>`, 'system');
        
        // Reset chart
        chartCouple.data.datasets[0].data = [];
        chartCouple.data.datasets[1].data = [];
        chartCouple.update();
        startTimeCouple = null; // Reset relative time
    });
});
