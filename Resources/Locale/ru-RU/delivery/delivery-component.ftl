delivery-recipient-examine = Этот предназначен для { $recipient }, { $job }.
delivery-already-opened-examine = Он уже был открыт.
delivery-earnings-examine = Доставка принесёт станции [color=yellow]{ $spesos }[/color] кредитов.
delivery-recipient-no-name = Безымянный
delivery-recipient-no-job = Неизвестно
delivery-unlocked-self = Вы открыли { $delivery } с помощью своего отпечатка пальца.
delivery-opened-self = Вы открыли { $delivery }.
delivery-unlocked-others = { CAPITALIZE($recipient) } открыл { $delivery } с помощью { POSS-ADJ($possadj) } отпечатка пальца.
delivery-opened-others = { CAPITALIZE($recipient) } открыл { $delivery }.
delivery-unlock-verb = Разблокировать
delivery-open-verb = Открыть
delivery-slice-verb = Вскрыть
delivery-teleporter-amount-examine =
    { $amount ->
        [one] Содержит [color=yellow]{ $amount }[/color] доставку.
       *[other] Содержит [color=yellow]{ $amount }[/color] доставок.
    }
delivery-teleporter-empty = { $entity } пуст(а).
delivery-teleporter-empty-verb = Забрать почту
# modifiers
delivery-priority-examine = Это [color=orange]приоритетный { $type }[/color]. У вас осталось [color=orange]{ $time }[/color] для доставки, чтобы получить бонус.
delivery-priority-expired-examine = Это [color=orange]приоритетный { $type }[/color]. Похоже, вы не успели в срок.
delivery-fragile-examine = Это [color=red]хрупкий { $type }[/color]. Доставьте его в целости, чтобы получить бонус.
delivery-fragile-broken-examine = Это [color=red]хрупкий { $type }[/color]. Он выглядит сильно повреждённым.
