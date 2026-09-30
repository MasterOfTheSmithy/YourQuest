# Runtime and authoritative player context

`YourQuestTutorialAutoBootstrap` constructs production services and enforces the startup/presentation gate. `YQInvestorPlayerMotor.ActiveMotor` is the authoritative player; bootstrap rejects duplicate player/motor paths and captures the configured collision/speed contract into `PlayerState`. `PlayerStateManager.state` owns logical player state while UI/camera/presentation components observe it. `PlayerProfile` remains a compatibility caller where current references prove it is needed.

Do not create a second player, motor, profile, inventory, equipment, or combat receiver. Input, camera, equipment, and tutorial fixture changes must preserve the one-player handoff and PlaySafe evidence.
