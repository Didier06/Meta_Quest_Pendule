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
    Chart.defaults.color = '#94a3b8';
    Chart.defaults.font.family = "'Inter', sans-serif";

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
                title: { display: true, text: 'Temps (s)', color: '#94a3b8' },
                grid: { color: 'rgba(255, 255, 255, 0.05)' },
                ticks: {
                    maxTicksLimit: 10,
                    callback: function(value) { return value.toFixed(1); }
                }
            },
            y: {
                title: { display: true, text: 'Angle (degrés)', color: '#94a3b8' },
                grid: { color: 'rgba(255, 255, 255, 0.05)' },
                suggestedMin: -90,
                suggestedMax: 90
            }
        },
        plugins: {
            legend: {
                labels: { color: '#e2e8f0', usePointStyle: true, boxWidth: 8 }
            },
            tooltip: {
                mode: 'index',
                intersect: false,
                backgroundColor: 'rgba(15, 23, 42, 0.9)',
                titleColor: '#f8fafc',
                bodyColor: '#e2e8f0',
                borderColor: 'rgba(255,255,255,0.1)',
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
        options: commonChartOptions
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
        options: commonChartOptions
    });

    // --- MQTT Logic ---
    let mqttClient = null;
    const globalStatusIndicator = document.getElementById('global-status-indicator');
    const globalStatusText = document.getElementById('global-status-text');
    const connectBtn = document.getElementById('mqtt-connect-btn');
    const disconnectBtn = document.getElementById('mqtt-disconnect-btn');
    const logContainer = document.getElementById('mqtt-log-container');

    // Auto-generate client ID
    document.getElementById('mqtt-client-id').value = 'lab_client_' + Math.random().toString(16).substr(2, 8);

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
        const port = document.getElementById('mqtt-port').value || 9001;
        const clientId = document.getElementById('mqtt-client-id').value;
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
                const topics = ['pendule/simple/state', 'pendule/couple/state'];
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
        // Expected JSON format: { "angle": 45.2, "time": 1.23 } 
        // or { "angle1": 30.1, "angle2": -15.4, "time": 1.23 }
        // If time is not provided, use local relative time.
        
        let relativeTime;

        if (topic === 'pendule/simple/state') {
            if (startTimeSimple === null) startTimeSimple = Date.now();
            relativeTime = data.time !== undefined ? data.time : (Date.now() - startTimeSimple) / 1000;
            
            if (data.angle !== undefined) {
                const dataset = chartSimple.data.datasets[0];
                dataset.data.push({ x: relativeTime, y: data.angle });
                
                if (dataset.data.length > MAX_DATA_POINTS) {
                    dataset.data.shift();
                }
                chartSimple.update('none'); // Update without animation
            }
        } 
        else if (topic === 'pendule/couple/state') {
            if (startTimeCouple === null) startTimeCouple = Date.now();
            relativeTime = data.time !== undefined ? data.time : (Date.now() - startTimeCouple) / 1000;
            
            let updated = false;
            if (data.angle1 !== undefined) {
                const dataset = chartCouple.data.datasets[0];
                dataset.data.push({ x: relativeTime, y: data.angle1 });
                if (dataset.data.length > MAX_DATA_POINTS) dataset.data.shift();
                updated = true;
            }
            if (data.angle2 !== undefined) {
                const dataset = chartCouple.data.datasets[1];
                dataset.data.push({ x: relativeTime, y: data.angle2 });
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
            cmd: "init",
            angle_init: parseFloat(document.getElementById('simple-angle').value),
            vitesse_init: parseFloat(document.getElementById('simple-vitesse').value),
            frottement: parseFloat(document.getElementById('simple-frottement').value),
            masse: parseFloat(document.getElementById('simple-masse').value),
            longueur: parseFloat(document.getElementById('simple-longueur').value)
        };

        const topic = 'pendule/simple/commande';
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
            cmd: "init",
            angle1_init: parseFloat(document.getElementById('couple-angle1').value),
            angle2_init: parseFloat(document.getElementById('couple-angle2').value),
            frottement: parseFloat(document.getElementById('couple-frottement').value),
            k: parseFloat(document.getElementById('couple-k').value),
            distance: parseFloat(document.getElementById('couple-distance').value)
        };

        const topic = 'pendule/couple/commande';
        mqttClient.publish(topic, JSON.stringify(payload));
        addLog(`Commande envoyée sur <span class="topic">${topic}</span>`, 'system');
        
        // Reset chart
        chartCouple.data.datasets[0].data = [];
        chartCouple.data.datasets[1].data = [];
        chartCouple.update();
        startTimeCouple = null; // Reset relative time
    });
});
