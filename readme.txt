github.com/drewf/SQL-Azure-Backup-Role


SQL Azure Backup Role v 0.1
Written in vb.net. Anyone willing to do a quick conversion to c# is invited to do so.
A restore service would be a nice compliment as well.


About SQLNCLI
Microsoft posts SQLNCLI is a redistributable insaller for SQL Server Native CLient installs. 

You need to install this via the Service Definition.
<Startup>
	<Task commandLine="installsqlncli.cmd" executionContext="elevated" taskType="background" />
</Startup>


For further SQLNCLI Info See:
http://msdn.microsoft.com/en-us/library/ms131321.aspx
http://www.microsoft.com/downloads/en/details.aspx?FamilyID=ceb4346f-657f-4d28-83f5-aae0c5c83d52

