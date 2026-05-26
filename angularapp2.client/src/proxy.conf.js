const PROXY_CONFIG = [
  {
    context: [
      "/api"
    ],
    // ИСПРАВЛЕНИЕ: Пишем 127.0.0.1 вместо localhost, чтобы избежать конфликта IPv4/IPv6
    target: "http://127.0.0.1:5101",
    secure: false,
    changeOrigin: true,
    logLevel: "debug"
  }
];

module.exports = PROXY_CONFIG;
