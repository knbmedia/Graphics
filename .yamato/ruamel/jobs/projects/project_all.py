from ruamel.yaml.scalarstring import DoubleQuotedScalarString as dss
from ..utils.namer import *
from ..utils.yml_job import YMLJob

class Project_AllJob():
    
    def __init__(self, project_name, editor, dependencies_in_all):
        self.project_name = project_name
        self.job_id = project_job_id_all(project_name, editor["version"])
        self.yml = self.get_job_definition(project_name, editor, dependencies_in_all).yml

    
    def get_job_definition(self, project_name, editor, dependencies_in_all):
    
        # define dependencies
        dependencies = []
        for d in dependencies_in_all:
            for test_platform_name in d["test_platform_names"]:
                
                file_name = project_filepath_specific(project_name, d["platform_name"], d["api_name"])
                job_id = project_job_id_test(project_name,d["platform_name"],d["api_name"],test_platform_name,editor["version"])

                dependencies.append(
                    {
                        'path' : f'{file_name}#{job_id}',
                        'rerun' : editor["rerun_strategy"]
                    }
                )

        # construct job
        job = YMLJob()
        job.set_name(f'All {project_name} CI - {editor["version"]}')
        job.add_dependencies(dependencies)
        job.add_var_custom_revision(editor["version"])
        return job
    
    