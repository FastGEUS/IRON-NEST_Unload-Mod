# IRON-NEST_Unload-Mod

A [MelonLoader](https://melonwiki.xyz/) mod for **Iron Nest: Heavy Turret Simulator** that lets you unload a loaded gun — return its powder charge and shell to the ammo cylinder — without firing it, per gun, independently of any other gun on the same turret.

## ⚠️ Current status: beta testing

This is my first mod. The core unload process works, but a few issues still needed fixing before this could go up on NexusMods for beta-testing.

**Known issues (fixed during alpha, kept here for history):**
- ✅ Reloading a second time after an unload was broken
- ✅ Unloading desynced the powder charge calculation, breaking shell range
- ✅ Left gun couldn't be unloaded while the right gun was ready to fire or mid-unload

This repository exists to track development and store working versions in case a change introduces a regression and I need to roll back to a known-good build. Please don't judge it too harshly — it's a learning project as much as a mod. If you hit a problem, **please open a GitHub [Issue](../../issues)** rather than a Nexus comment — it's much easier for me to keep track of bugs that way, and I'll usually ask for your `Latest.log` file.

— Svet

## Why this mod exists

By default, once a shell and powder charge are loaded into a gun, the only way to clear it is to fire. If you loaded the wrong shell type, picked the wrong powder charge, or just changed your mind about a firing solution, you were stuck wasting a real shot. This mod adds a proper "abort reload" action per gun.

## Features

- **Per-gun unload**, fully independent — unloading the left gun has no effect on the right gun's state or timing, and vice versa.
- **Keyboard-only hotkeys** — deliberately *not* mouse-clickable buttons. See [Why no clickable buttons?](#why-no-clickable-buttons) below.
- **User-remappable hotkeys** via a plain-text config file — no recompiling, no code editing.
- **Realistic unload behavior**: triggers the gun's own ~12 second reset animation, then returns the shell to the cylinder and rolls a chance to fully recover the powder charge (partial loss otherwise) — see [Unload mechanics](#unload-mechanics).
- **Duplicate-click / duplicate-hotkey safe** — pressing the hotkey again while a gun is already mid-unload is ignored (logged, not crashed).
- On-screen hint showing current hotkey bindings, toggleable on/off.

## Default hotkeys

| Action | Default key |
|---|---|
| Unload left gun | `8` |
| Unload right gun | `9` |
| Toggle on-screen hint | `0` |

## Requirements

- **Iron Nest: Heavy Turret Simulator** (IL2CPP build)
- [MelonLoader](https://melonwiki.xyz/) 0.7.3 (Open-Beta) or compatible, .NET 6 runtime
- No other mod dependencies

## Build

```
dotnet build -c Release
```

The `.csproj` copies the built DLL straight into the game's `Mods` folder for you (see the `CopyToMods` target) — just point `IronNestPath` at your install location first.

**Do not download or build this mod for general use until it's published in [Releases](../../releases) or on NexusMods** — anything on the `main` branch right now is a development snapshot, not a tested build.

## Installation (once released)

1. Install MelonLoader for Iron Nest: Heavy Turret Simulator if you haven't already.
2. Download the latest release `IronNestGunMod.dll`.
3. Drop it into the game's `Mods` folder (`<game folder>\Mods\`).
4. Launch the game. The mod creates its config section automatically on first launch — see [Configuration](#configuration).

## Configuration

On first launch, the mod creates (or updates) a section in:

```
UserData\MelonPreferences.cfg
```

```ini
[PerGunUnloadButtons]
LeftGunKey = Digit8
RightGunKey = Digit9
ToggleHintKey = Digit0
```

To remap a key, edit the value to any valid `UnityEngine.InputSystem.Key` enum name (examples: `F6`, `Numpad8`, `LeftShift`, `Q`). Invalid names fall back to the built-in default for that entry and log a warning — they will never crash the mod or disable the others.

**Changes take effect after restarting the game** (MelonPreferences is read once at startup).

## Unload mechanics

Pressing a gun's hotkey while it is loaded and able to fire:

1. Resets the gun's reload state machine and plays its normal ~12 second reset/reload animation.
2. Resets barrel elevation and powder dispenser levers.
3. After the animation finishes, returns the previously chambered shell to the cylinder.
4. Rolls a chance (currently ~43%) to fully recover the powder charge that was loaded; otherwise recovers all but one charge.

While a gun is mid-unload, its hotkey is ignored if pressed again (no stacking/duplicate unloads), and its charge state is not touched by the mod's background sync until the sequence completes.

## Why no clickable buttons?

Earlier builds used on-screen `OnGUI` buttons. During real gameplay, the game locks and hides the system cursor while the player is aiming a turret (`Cursor.lockState == Locked`), which causes Unity to report the mouse position as an off-screen sentinel value. Under that condition, **no on-screen button can ever register a click** — confirmed by diagnostic logging during development. Since aiming is the normal state during play, buttons were unreliable in exactly the situations where the mod is most useful. Keyboard hotkeys have no such limitation, so the mod is now keyboard-only, with a plain on-screen text hint (not a button) showing current bindings.

## Known limitations / roadmap

- Currently supports exactly two guns per turret, matched by `"Left"` / `"Right"` in their in-game object name. Additional guns without either substring in their name are still unloaded correctly but currently share no dedicated hotkey slot.
- The reset animation duration and powder-recovery odds are fixed constants for now (see `ModConfig`); not yet user-configurable.
- No BepInEx build yet — MelonLoader only for now.
- Reload/unload debug logging is currently verbose (every step logs success or failure) to aid ongoing bug-fixing; will be trimmed down for a quieter default in a future release.

**Planned:**
- BepInEx-compatible build.
- Improved/expanded unload animation and feedback.
- Ongoing compatibility maintenance across game updates.

**Other ideas I'm considering (no promises):**
- Configurable unload duration and recovery odds, instead of hardcoded constants.
- Support for turrets with more than 2 guns, with hotkeys assignable per gun rather than just Left/Right.
- Some kind of sound or flash when unload starts/completes, since there's no button to visually "press" anymore.
- A "quiet mode" log setting to cut the verbose per-step logging down for players who never hit issues.
- A startup self-check that warns clearly if a game update changed something the mod depends on, instead of failing with a raw exception.
- In-game key-rebinding prompt as an alternative to hand-editing the config file.

## Bug reports

Please use the [Issues](../../issues) tab here on GitHub rather than a Nexus comment — it's much easier for me to keep track of things that way. Including your `Latest.log` (from `MelonLoader\Logs\`) with any report helps a lot.

## Credits

Developed by Svet — my first mod, so please go easy on me. Built against MelonLoader 0.7.3 Open-Beta / Unity 6000.3.21f1 (IL2CPP).

---

# (Русская версия)

Мод для [MelonLoader](https://melonwiki.xyz/) для **Iron Nest: Heavy Turret Simulator**, который позволяет разрядить заряженное орудие — вернуть порох и снаряд обратно в барабан — без выстрела, отдельно для каждого орудия, независимо от состояния других орудий на той же турели.

## ⚠️ Текущий статус: бета-тестирование

Это мой первый мод. Основной механизм разряда работает, но перед выкладкой на NexusMods для бета-теста нужно было закрыть несколько проблем.

**Известные проблемы (исправлены во время альфы, оставлены здесь для истории):**
- ✅ Повторная зарядка после разряда была сломана
- ✅ Разряд рассинхронизировал расчёт заряда пороха, что ломало дальность стрельбы снаряда
- ✅ Левое орудие не разряжалось, пока правое было готово к выстрелу или само разряжалось

Этот репозиторий существует, чтобы отслеживать разработку и хранить рабочие версии на случай, если новое изменение внесёт регрессию и понадобится откатиться к последней стабильной сборке. Пожалуйста, не судите слишком строго — это скорее учебный проект, чем полноценный мод. Если что-то не работает, **пожалуйста, заводите GitHub [Issue](../../issues)**, а не пишите в комментариях на Nexus — так мне сильно проще уследить за багами, и я, как правило, попрошу твой файл `Latest.log`.

— Svet

## Зачем этот мод нужен

По умолчанию, если в орудие загружены снаряд и порох, единственный способ его "очистить" — выстрелить. Если ты загрузил не тот тип снаряда, выбрал не тот заряд пороха, или просто передумал насчёт решения на стрельбу — тебе приходилось тратить настоящий выстрел впустую. Этот мод добавляет нормальное действие "отмена зарядки" для каждого орудия отдельно.

## Возможности

- **Разряд каждого орудия по отдельности**, полностью независимо — разряд левого орудия никак не влияет на состояние или таймер правого, и наоборот.
- **Только клавиатурные горячие клавиши** — намеренно без кликабельных кнопок мышью. Почему — смотри раздел ниже.
- **Горячие клавиши можно переназначить** через обычный текстовый конфиг-файл — без перекомпиляции и правки кода.
- **Реалистичное поведение разряда**: запускает настоящую ~12-секундную анимацию сброса орудия, затем возвращает снаряд в барабан и с некоторым шансом полностью восстанавливает заряд пороха (иначе — частичная потеря).
- **Защита от повторных нажатий** — если нажать горячую клавишу ещё раз, пока орудие уже разряжается, нажатие просто игнорируется (с записью в лог, без падения мода).
- Текстовая подсказка на экране с текущими горячими клавишами, можно скрыть/показать.

## Горячие клавиши по умолчанию

| Действие | Клавиша по умолчанию |
|---|---|
| Разрядить левое орудие | `8` |
| Разрядить правое орудие | `9` |
| Показать/скрыть подсказку | `0` |

## Требования

- **Iron Nest: Heavy Turret Simulator** (сборка IL2CPP)
- [MelonLoader](https://melonwiki.xyz/) 0.7.3 (Open-Beta) или совместимая версия, .NET 6
- Никаких других модов не требуется

## Сборка

```
dotnet build -c Release
```

`.csproj` сам копирует собранную DLL в папку `Mods` игры (см. таргет `CopyToMods`) — просто укажи путь к игре в `IronNestPath`.

**Не скачивай и не собирай этот мод для обычного использования, пока он не появится в [Releases](../../releases) или на NexusMods** — всё, что сейчас лежит в ветке `main`, это черновые сборки для разработки, а не проверенные версии.

## Установка (после релиза)

1. Установи MelonLoader для Iron Nest: Heavy Turret Simulator, если ещё не установлен.
2. Скачай последний релиз `IronNestGunMod.dll`.
3. Положи файл в папку `Mods` игры (`<папка игры>\Mods\`).
4. Запусти игру. Мод сам создаст секцию настроек при первом запуске — см. раздел "Настройка".

## Настройка

При первом запуске мод создаёт (или обновляет) секцию в файле:

```
UserData\MelonPreferences.cfg
```

```ini
[PerGunUnloadButtons]
LeftGunKey = Digit8
RightGunKey = Digit9
ToggleHintKey = Digit0
```

Чтобы переназначить клавишу, впиши любое допустимое название из перечисления `UnityEngine.InputSystem.Key` (примеры: `F6`, `Numpad8`, `LeftShift`, `Q`). Если название неверное — используется дефолт для этой конкретной клавиши, с предупреждением в лог; мод не упадёт и не отключит остальные настройки.

**Изменения применяются после перезапуска игры** (MelonPreferences читается один раз при старте).

## Механика разряда

При нажатии горячей клавиши орудия, если оно заряжено и готово к выстрелу:

1. Сбрасывается состояние машины перезарядки орудия, запускается обычная ~12-секундная анимация сброса/перезарядки.
2. Сбрасываются угол возвышения ствола и рычаги пороховых зарядов.
3. После завершения анимации снаряд возвращается в барабан.
4. С некоторым шансом (сейчас ~43%) полностью восстанавливается заряд пороха; иначе восстанавливается на один заряд меньше.

Пока орудие разряжается, повторное нажатие его горячей клавиши игнорируется (без накопления/дублирования разряда), а фоновая синхронизация заряда мода не трогает его состояние до завершения всей последовательности.

## Почему без кликабельных кнопок?

В более ранних версиях использовались кнопки `OnGUI` на экране. Во время реальной игры игра блокирует и скрывает системный курсор, когда игрок прицеливается турелью (`Cursor.lockState == Locked`), из-за чего Unity сообщает позицию мыши как служебное значение за пределами экрана. В этом состоянии **никакая кнопка на экране не может зарегистрировать клик** — это подтверждено диагностическим логированием во время разработки. Поскольку прицеливание — обычное состояние во время игры, кнопки были ненадёжны именно в тех ситуациях, где мод нужнее всего. У клавиатурных горячих клавиш такого ограничения нет, поэтому теперь мод работает только через клавиатуру, а на экране осталась простая текстовая подсказка (не кнопка) с текущими биндами.

## Известные ограничения / планы

- Сейчас поддерживаются ровно два орудия на турель, определяемые по подстроке `"Left"` / `"Right"` в имени объекта в игре. Дополнительные орудия без этих подстрок в имени всё равно корректно разряжаются, но пока не имеют отдельного слота горячей клавиши.
- Длительность анимации сброса и шанс восстановления пороха сейчас — фиксированные константы (см. `ModConfig`), пока не настраиваются пользователем.
- Сборки для BepInEx пока нет — только MelonLoader.
- Отладочное логирование разряда сейчас подробное (каждый шаг логирует успех или ошибку) — это помогает в текущей отладке багов, в будущем релизе будет приглушено по умолчанию.

**В планах:**
- Сборка, совместимая с BepInEx.
- Улучшенная/расширенная анимация и обратная связь при разряде.
- Постоянная поддержка совместимости при обновлениях игры.

**Другие идеи на будущее (без обещаний):**
- Настраиваемая длительность разряда и шанс восстановления вместо жёстких констант.
- Поддержка турелей с более чем 2 орудиями, с горячими клавишами не только для Left/Right.
- Звук или вспышка при начале/завершении разряда, раз кнопки для "нажатия" больше нет.
- "Тихий режим" логирования для игроков, у которых всё работает без проблем.
- Проверка совместимости при запуске, которая явно предупредит, если обновление игры изменило что-то, от чего зависит мод, вместо падения с голым исключением.
- Подсказка для перебиндинга клавиш прямо в игре, как альтернатива правке конфиг-файла руками.

## Баг-репорты 

Пожалуйста, используй вкладку [Issues](../../issues) здесь, на GitHub, а не комментарии на Nexus — так мне сильно проще следить за багами. Приложенный файл `Latest.log` (из папки `MelonLoader\Logs\`) очень помогает.

## Благодарности

Разработано Svet — это мой первый мод, так что не судите слишком строго. Собрано на MelonLoader 0.7.3 Open-Beta / Unity 6000.3.21f1 (IL2CPP).
