reagent-effect-condition-guidebook-total-damage =
    { $max ->
        [2147483648] у него как минимум { NATURALFIXED($min, 2) } общего урона
       *[other]
            { $min ->
                [0] у него не более { NATURALFIXED($max, 2) } общего урона
               *[other] у него от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } общего урона
            }
    }
reagent-effect-condition-guidebook-type-damage =
    { $max ->
        [2147483648] у него как минимум { NATURALFIXED($min, 2) } { $type } урона
       *[other]
            { $min ->
                [0] у него не более { NATURALFIXED($max, 2) } { $type } урона
               *[other] у него от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } { $type } урона
            }
    }
reagent-effect-condition-guidebook-group-damage =
    { $max ->
        [2147483648] у него как минимум { NATURALFIXED($min, 2) } { $type } урона.
       *[other]
            { $min ->
                [0] у него не более { NATURALFIXED($max, 2) } { $type } урона.
               *[other] у него от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } { $type } урона
            }
    }
reagent-effect-condition-guidebook-total-hunger =
    { $max ->
        [2147483648] у цели как минимум { NATURALFIXED($min, 2) } общего голода
       *[other]
            { $min ->
                [0] у цели не более { NATURALFIXED($max, 2) } общего голода
               *[other] у цели от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } общего голода
            }
    }
reagent-effect-condition-guidebook-reagent-threshold =
    { $max ->
        [2147483648] присутствует как минимум { NATURALFIXED($min, 2) }u { $reagent }
       *[other]
            { $min ->
                [0] присутствует не более { NATURALFIXED($max, 2) }u { $reagent }
               *[other] присутствует от { NATURALFIXED($min, 2) }u до { NATURALFIXED($max, 2) }u { $reagent }
            }
    }
reagent-effect-condition-guidebook-mob-state-condition = моб находится в состоянии { $state }
reagent-effect-condition-guidebook-job-condition = профессия цели — { $job }
reagent-effect-condition-guidebook-solution-temperature =
    температура раствора { $max ->
        [2147483648] не менее { NATURALFIXED($min, 2) }K
       *[other]
            { $min ->
                [0] не более { NATURALFIXED($max, 2) }K
               *[other] от { NATURALFIXED($min, 2) }K до { NATURALFIXED($max, 2) }K
            }
    }
reagent-effect-condition-guidebook-body-temperature =
    температура тела { $max ->
        [2147483648] не менее { NATURALFIXED($min, 2) }K
       *[other]
            { $min ->
                [0] не более { NATURALFIXED($max, 2) }K
               *[other] от { NATURALFIXED($min, 2) }K до { NATURALFIXED($max, 2) }K
            }
    }
reagent-effect-condition-guidebook-organ-type =
    расой цели { $shouldhave ->
        [true] является
       *[false] не является
    } { INDEFINITE($name) } { $name } органом
reagent-effect-condition-guidebook-has-tag =
    у цели { $invert ->
        [true] нет
       *[false] есть
    } тега { $tag }
reagent-effect-condition-guidebook-this-reagent = этот реагент
reagent-effect-condition-guidebook-breathing =
    цель { $isBreathing ->
        [true] дышит нормально
       *[false] задыхается
    }
reagent-effect-condition-guidebook-internals =
    цель { $usingInternals ->
        [true] использует внутренние органы
       *[false] дышит атмосферным воздухом
    }
