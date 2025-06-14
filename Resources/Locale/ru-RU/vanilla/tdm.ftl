tdm-preset-title = Командный дезматч
tdm-preset-description = Убей КРАСНЫХ! Если ты синий... ну и наоборот иначе.
tdm-firstblood = { $player } проливает первую кровь убив { $victim }!
tdm-announcer = Коментирует Ванилька
tdm-killstreak =
    { $player } убил { $victim }.
    { $streak ->
        [5] БУЙСТВО! КТО-НИБУДЬ ОСТАНОВИТЕ ЕГО! КТО-НИБУДЬ ВООБЩЕ ЖИВ?!
        [4] СЕРИЯ УБИЙСТВ! 4 ЖЕРТВА НА ЕГО СЧЕТУ!
        [3] Серия убийств! Тройное убийство!
        [2] Двойное убийство!
       *[1] { "" }
    }
tdm-gameover =
    Бой окончен!  { $winner ->
        [false] Победа синей команды!
        [true] Победа красной команды!
       *[other] Ничья :c
    }
    { $result }
TDM-NotAvailable = TDM
TDM-Available = TDM { $blueguys } VS { $redguys } ({ $timer})
accept-TDM-window-title = Приглашение в TDM
accept-TDM-window-prompt-text-part = TDM будет начат через 30 секунд, хотите принять участие?
accept-TDM-window-accept-button = Да!
accept-TDM-window-deny-button = Нет
