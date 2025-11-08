entity-condition-guidebook-total-damage =
    { $max ->
        [2147483648] имеет не менее { NATURALFIXED($min, 2) } общего урона
       *[other]
            { $min ->
                [0] имеет не более { NATURALFIXED($max, 2) } общего урона
               *[other] имеет от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } общего урона
            }
    }
entity-condition-guidebook-type-damage =
    { $max ->
        [2147483648] имеет не менее { NATURALFIXED($min, 2) } { $type } урона
       *[other]
            { $min ->
                [0] имеет не более { NATURALFIXED($max, 2) } { $type } урона
               *[other] имеет от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } { $type } урона
            }
    }
entity-condition-guidebook-group-damage =
    { $max ->
        [2147483648] имеет не менее { NATURALFIXED($min, 2) } { $type } урона
       *[other]
            { $min ->
                [0] имеет не более { NATURALFIXED($max, 2) } { $type } урона
               *[other] имеет от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } { $type } урона
            }
    }
entity-condition-guidebook-total-hunger =
    { $max ->
        [2147483648] цель имеет не менее { NATURALFIXED($min, 2) } единиц голода
       *[other]
            { $min ->
                [0] цель имеет не более { NATURALFIXED($max, 2) } единиц голода
               *[other] цель имеет от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } единиц голода
            }
    }
entity-condition-guidebook-reagent-threshold =
    { $max ->
        [2147483648] содержит не менее { NATURALFIXED($min, 2) } ед. { $reagent }
       *[other]
            { $min ->
                [0] содержит не более { NATURALFIXED($max, 2) } ед. { $reagent }
               *[other] содержит от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } ед. { $reagent }
            }
    }
entity-condition-guidebook-mob-state-condition = моб находится в состоянии { $state }
entity-condition-guidebook-job-condition = работа цели — { $job }
entity-condition-guidebook-solution-temperature =
    температура раствора { $max ->
        [2147483648] не ниже { NATURALFIXED($min, 2) } K
       *[other]
            { $min ->
                [0] не выше { NATURALFIXED($max, 2) } K
               *[other] от { NATURALFIXED($min, 2) } K до { NATURALFIXED($max, 2) } K
            }
    }
entity-condition-guidebook-body-temperature =
    температура тела { $max ->
        [2147483648] не ниже { NATURALFIXED($min, 2) } K
       *[other]
            { $min ->
                [0] не выше { NATURALFIXED($max, 2) } K
               *[other] от { NATURALFIXED($min, 2) } K до { NATURALFIXED($max, 2) } K
            }
    }
entity-condition-guidebook-organ-type =
    метаболизирующий орган { $shouldhave ->
        [true] является
       *[false] не является
    } органом типа { INDEFINITE($name) } { $name }
entity-condition-guidebook-has-tag =
    цель { $invert ->
        [true] не имеет
       *[false] имеет
    } тег { $tag }
entity-condition-guidebook-this-reagent = этот реагент
entity-condition-guidebook-breathing =
    метаболизатор { $isBreathing ->
        [true] дышит нормально
       *[false] задыхается
    }
entity-condition-guidebook-internals =
    метаболизатор { $usingInternals ->
        [true] использует внутреннюю подачу воздуха
       *[false] дышит атмосферным воздухом
    }
