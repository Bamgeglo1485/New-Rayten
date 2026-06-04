<!-- Логотип (опционально) -->
<!--
<p align="center">
  <img alt="Rayten" width="880" height="300" src="https://raw.githubusercontent.com/space-wizards/asset-dump/de329a7898bb716b9d5ba9a0cd07f38e61f1ed05/github-logo.svg" />
</p>
-->
## 🚀 О проекте
**New Rayten** (рабочее название) — это репозиторий сборки для русскоязычного сервера *Space Station 14* на базе ныне закрытого сервера Rayten.
Ранее часть кода(эксклюзивный контент от прошлого хоста) был закрыт, но отныне репозиторий полностью публичный и его можно использовать для своих проектов. Только соблюдайте лицензию AGPL-3.0 по отношению некоторого контента, подробности о лицензиях ниже.

![Partial Code](https://img.shields.io/badge/исходный%20код-apgl%203.0-orange?style=for-the-badge&logo=github)

---

## 🔗 Полезные ссылки
- 🌐 [Наш Discord]([https://discord.gg/W3Ep2esrzc](https://discord.gg/G6uYR3Hq5B))
- 📖 [Наша Вики](пока отсутствует)
- 💾 [Официальный репозиторий SS14](https://github.com/space-wizards/space-station-14)

---

## 📚 Документация
Для работы с проектом советуем ознакомиться с [официальной документацией](https://docs.spacestation14.io/).
Там собрана информация о:
- движке и его возможностях,
- контенте SS14,
- дизайне игры,
- гайдах для начинающих разработчиков.

---

## 🛠️ Сборка проекта

### Быстрый старт
1. Склонируйте репозиторий:
   ```bash
   git clone https://github.com/VanillaStation14/VanillaStation.git
   cd VanillaStation
    ```
2. Запустите скрипт инициализации:
   ```bash
    python ./RUN_THIS.py
    ```
3. Скомпилируйте проект (пример для Windows):
   ```bash
    dotnet build Content.Packaging --configuration Release
    ```
🔎 Более подробная инструкция — [здесь](https://docs.spacestation14.com/en/general-development/setup.html)

## ⚖️ Лицензия
Весь код, который не разработан Space Wizards Federation или не взаимствован с других репозиториев распространяется под лицензией AGPL-3.0, то есть код можно взаимствовать только для открытых репозиториев.
