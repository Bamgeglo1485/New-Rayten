## UI

injector-volume-transfer-label =
    Объём: [color=white]{ $currentVolume }/{ $totalVolume }u[/color]
    Режим: [color=white]{ $modeString }[/color] ([color=white]{ $transferVolume }ед.[/color])
injector-toggle-verb-text = Переключить режим
injector-component-inject-mode-name = введение
injector-component-draw-mode-name = забор
injector-component-dynamic-mode-name = динамический
injector-component-mode-changed-text = Текущий: { $mode }
injector-volume-label =
    Объём: [color=white]{ $currentVolume }/{ $totalVolume }[/color]
    Режим: [color=white]{ $modeString }[/color] ([color=white]{ $transferVolume } ед.[/color])

## Entity

injector-component-cannot-transfer-message = Вы не можете ничего переместить в { $target }!
injector-component-cannot-transfer-message-self = Вы не можете ничего переместить в себя!
injector-component-cannot-draw-message = Вы не можете ничего набрать из { $target }!
injector-component-cannot-draw-message-self = Вы не можете ничего набрать из себя!
injector-component-cannot-inject-message = Вы не можете ничего ввести в { $target }!
injector-component-cannot-inject-message-self = Вы не можете ничего ввести в себя!
injector-component-inject-success-message = Вы вводите { $amount }ед. в { $target }!
injector-component-inject-success-message-self = Вы вводите { $amount }ед. в себя.
injector-component-cannot-toggle-dynamic-message = Невозможно переключить динамический режим!
injector-component-empty-message = { CAPITALIZE(THE($injector)) } пустой!
injector-component-blocked-user = Защитная экипировка заблокировала вашу инъекцию!
injector-component-blocked-other = { CAPITALIZE(THE(POSS-ADJ($target))) } броня заблокировала инъекцию { THE($user) }!
injector-component-transfer-success-message = Вы перемещаете { $amount } ед. в { $target }.
injector-component-transfer-success-message-self = Вы перемещаете { $amount } ед. в себя.
injector-component-draw-success-message = Вы набираете { $amount } ед. из { $target }.
injector-component-draw-success-message-self = Вы набираете { $amount } ед. из себя.
injector-component-target-already-full-message = { $target } полон!
injector-component-target-already-full-message-self = Вы полны!
injector-component-ignore-mobs = Этот инжектор может взаимодействовать только с контейнерами!
injector-component-target-is-empty-message = { $target } пуст!
injector-component-needle-injecting-user = Вы начинаете вводить иглу.
injector-component-needle-injecting-target = { CAPITALIZE(THE($user)) } пытается ввести вам иглу!
injector-component-needle-drawing-user = Вы начинаете набирать содержимое иглой.
injector-component-needle-drawing-target = { CAPITALIZE(THE($user)) } пытается набрать у вас содержимое иглой!
injector-component-spray-injecting-user = Вы начинаете готовить распылитель.
injector-component-spray-injecting-target = { CAPITALIZE(THE($user)) } пытается установить распылитель на вас!
injector-component-target-is-empty-message-self = Вы пусты!
injector-component-feel-prick-message = Вы почувствовали лёгкий укол!
injector-component-cannot-toggle-draw-message = Больше не набрать!
injector-component-cannot-toggle-inject-message = Нечего вводить!

injector-component-inject-target-protected = Нельзя сделать инъекцию, так как он(а) защищен(а) броней!
