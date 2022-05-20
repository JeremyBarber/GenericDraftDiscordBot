# GenericDraftDiscordBot
A Discord bot for running generic drafts across multiple users

# CLI

`!DraftHelp`
Shows details of the available commands

`!DraftSetup [HandSize <int>] [BankSize <int>] [Description <string>]`
Register a new Draft and define the initial config

`!DraftItems [ID <string>] [Items <.csv attachment>]`
Set what items will be used for a particular Draft. Attachment must be .csv with header row and first column as a unique id

`!DraftStart [ID <string>]`
Begin the Draft

`!DraftCancel [ID <string>]`
Stop and delete a Draft

`!DraftStatus [ID <string>]`
Provide information about the state of a Draft

# Required Permissions

- Manage Channels
- Read Messages/View Channels
- Manage Events
- Send Messages
- Add Reactions

# Workarounds

- Because Discord seemingly does not emit Iteraction events of any kind from DM channels. So, user drafts are currently performed in Private Channels. This adds a lot of churn and code, but has the benefit of actually working

- Comparing users is done with a custom comparer because the different User object interfaces are not comparable to each other even when representing the same users.

# Future work

- Better lifecycle control so it can run indefinitely without thread leaks

- Add a config update command
