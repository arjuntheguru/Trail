import { taskActions } from "./task-slice";
import { tokenConfig } from "../utils/tokenUtility";

import { notifyError, notifySuccess } from '../components/Toast/Toast';

import API from '../utils/api';


export const addTask = (item, callback = () => {}) => dispatch => {

    dispatch(taskActions.start_loading());

    let config = tokenConfig();

    API.post('api/task', item, config)
        .then(res => {
            dispatch(taskActions.add_task_success(res.data)); 
            callback();
            notifySuccess(res.data.message);           
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(taskActions.error());
            notifyError("Error occured while adding Task");
        })
};

export const updateTask = (item) => dispatch => {

    dispatch(taskActions.start_loading());

    let config = tokenConfig();
    console.log(item);
    API.put('api/task', item, config)
        .then(res => {
            console.log(res);
            dispatch(taskActions.update_task_sucess(res.data));            
            notifySuccess("Task updated successfully");           
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(taskActions.error());
            notifyError("Error occured while adding Task");
        })
};

export const deleteTask = (item) => dispatch => {

    dispatch(taskActions.start_loading());

    let config = tokenConfig();
    console.log(item);
    API.patch(`api/task/${item.id}`, config)
        .then(res => {
            console.log(res);
            dispatch(taskActions.update_task_sucess(res.data));            
            notifySuccess("Task deleted successfully");           
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(taskActions.error());
            notifyError("Error occured while adding Task");
        })
};

export const getTask = (item = { pageNumber: 1, pageSize: 10 }) => (dispatch) => {

    dispatch(taskActions.start_loading());
    console.log(item);
    let config = tokenConfig();

    API.get(`api/task?pageNumber=${item.pageNumber}&pageSize=${item.pageSize}`, config)
        .then(res => {
            dispatch(taskActions.load_task_success(res.data));
        })
        .catch(err => {
            console.log(err.response);
            dispatch(taskActions.error());
        });
};