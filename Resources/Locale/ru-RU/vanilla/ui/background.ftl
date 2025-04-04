background-ui-SkillsLabel-prefix= [bold]Навыки:[/bold] { $skills }
background-ui-EasySkills=[color={ $skilltype ->
        [Piloting] #85490c
        [Botany] #6db33f
        [MusInstruments] #355f44
        [Bureaucracy] #939794
        [Atmosphere] #4ebed7
       *[other] white
    }]+ { $skilltype ->
        [Piloting] Пилотирование
        [Botany] Ботаника
        [MusInstruments] Муз. инструменты
        [Bureaucracy] Бюрократия
        [Atmosphere] Атмосфера
       *[other] ???
    }[/color]
background-ui-Skills= [color={ $skilltype ->
        [Piloting] #85490c
        [RangeWeapon] #a90000
        [MeleeWeapon] #ed4646
        [Medicine] #005b53
        [Chemistry] #AD4915
        [Engineering] #ff6600
        [Building] #FFBF00
        [Research] #c02dc8
        [Instrumentation] #b03bd0
        [Botany] #6db33f
        [MusInstruments] #355f44
        [Bureaucracy] #939794
        [Atmosphere] #4ebed7
        [Crime] #ff0000
       *[other] white
    }]{ $skilltype ->
        [Piloting] пилотирование
        [RangeWeapon] стрельба
        [MeleeWeapon] ближний бой
        [Medicine] медицина
        [Chemistry] химия
        [Engineering] инженерия
        [Building] строительство
        [Research] исследование
        [Instrumentation] Приборостроение
        [Botany] Ботаника
        [MusInstruments] Муз. инструменты
        [Bureaucracy] Бюрократия
        [Atmosphere] Атмосфера
        [Crime] Преступность
       *[other] ???
    }[/color]: { $lvl }
background-ui-SpecialsLabel-prefix=[bold]Особое:[/bold] { $specials }
background-ui-SpecialsLabel-special= [color={ $special ->
        [MakeAntag] #d01212
        [MakeNonAntag] #12a423
        [MakeFreeAgent] #ecf000
        [RandomMagic] #a448e2
       *[other] white
    }]{ $special ->
        [MakeAntag] Антагонист
        [MakeNonAntag] Не антагонист
        [MakeFreeAgent] Свободный агент
        [RandomMagic] Случайное заклинание
       *[other] ???
    }[/color]